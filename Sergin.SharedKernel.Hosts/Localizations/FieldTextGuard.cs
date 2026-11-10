using System.Reflection;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Hosts.Localizations;

/// <summary>
/// The field-text guard (spec decision 16), run at host start. For every configured request type in a module's
/// Contracts assembly (local and remote), each public instance property a person may see — not marked
/// <c>NotShown(...)</c> in its configuration, and not a paging or criteria record (<see cref="Paggination"/>,
/// <see cref="Sorting"/>, <see cref="Filtering"/>, <see cref="Term"/>) — must have a <see cref="FieldText"/> in the
/// request's category; a request with such a property and no category is refused itself. The target ids a typed
/// record's target interface declares (<c>Id</c>, <c>RootId</c>, <c>ExpectedCount</c>) are never shown, so need no
/// <c>NotShown(...)</c>; a parent id (<c>ParentId</c>, <c>NewParentId</c>) is a reference a person picks, and stays shown. And the reverse of decision 7:
/// every field of a <see cref="ListFieldSet"/> declared beside a configured list query must have a field text in that
/// query's category, so a list column or filter chip never lacks a label. One refusal lists everything missing.
/// </summary>
/// <remarks>
/// A request with no configuration is out of scope: it has no category to read, and its properties never reach a
/// person through the catalog (ProvisionExternalUserCommand runs inside the OIDC callback).
/// </remarks>
public sealed class FieldTextGuard(IEnumerable<Assembly> contractsAssemblies)
{
    private static readonly HashSet<Type> criteriaTypes = [typeof(Paggination), typeof(Sorting), typeof(Filtering), typeof(Term)];

    private static readonly HashSet<string> targetIds = new(["Id", "RootId", "ExpectedCount"], StringComparer.Ordinal);

    private readonly Assembly[] assemblies = [.. contractsAssemblies.Distinct()];

    /// <summary>The Contracts assemblies whose configured requests and list field sets <see cref="EnsureLabelled"/> checks.</summary>
    public IReadOnlyCollection<Assembly> Assemblies => assemblies;

    public void EnsureLabelled(CommandConfigurationRegistry registry, TextCatalog catalog) =>
        Check(assemblies.SelectMany(assembly => assembly.GetTypes()), registry, catalog);

    /// <summary>Checks the configured requests and the list field sets among <paramref name="candidateTypes"/>.</summary>
    public static void Check(IEnumerable<Type> candidateTypes, CommandConfigurationRegistry registry, TextCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(candidateTypes);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(catalog);

        Type[] candidates = [.. candidateTypes.Distinct()];
        HashSet<Type> configured = [.. registry.ConfiguredTypes];
        Type[] requests = [.. candidates.Where(configured.Contains)];

        List<string> missing = [];

        foreach (Type request in requests)
        {
            missing.AddRange(MissingFields(request, registry, catalog));
        }

        foreach ((Type declaringType, ListFieldSet set) in ListFieldSets(candidates))
        {
            missing.AddRange(MissingListFields(declaringType, set, requests, registry, catalog));
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "A field a person sees has no FieldText: " + string.Join("; ", missing.Order(StringComparer.Ordinal)) + ". "
                + "Declare a FieldText of the property's name in the request's category (its configuration's Texts(...), "
                + "or its bound root's), or mark the property NotShown(...) in its configuration.");
        }
    }

    private static IEnumerable<string> MissingFields(Type request, CommandConfigurationRegistry registry, TextCatalog catalog)
    {
        IReadOnlySet<string> hidden = registry.HiddenProperties(request);
        IReadOnlySet<string> declaredByType = CommandTypes.InterfaceMemberNames(request);

        string[] shown =
        [
            .. request.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Where(property => !criteriaTypes.Contains(Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType))
                .Select(property => property.Name)
                .Where(name => !hidden.Contains(name))
                .Where(name => !(targetIds.Contains(name) && declaredByType.Contains(name)))
                .Distinct(StringComparer.Ordinal),
        ];

        if (shown.Length == 0)
        {
            return [];
        }

        if (registry.CategoryOf(request) is not { } category)
        {
            return
            [
                $"{request.FullName} (no text category for {string.Join(", ", shown)}: name one with Texts(...) or bind the "
                + "configuration to its aggregate)",
            ];
        }

        return shown.Where(name => catalog.FindField(category, name) is null).Select(name => $"{request.FullName}.{name}");
    }

    private static IEnumerable<string> MissingListFields(
        Type declaringType, ListFieldSet set, Type[] requests, CommandConfigurationRegistry registry, TextCatalog catalog)
    {
        TextCategory[] categories =
        [
            .. requests
                .Where(request => request.Namespace == declaringType.Namespace && typeof(ListQuery).IsAssignableFrom(request))
                .Select(registry.CategoryOf)
                .OfType<TextCategory>()
                .DistinctBy(category => category.Prefix, StringComparer.Ordinal),
        ];

        if (categories.Length == 0)
        {
            return set.Fields.Count == 0
                ? []
                : [$"{declaringType.FullName} (no configured list query with a text category in {declaringType.Namespace})"];
        }

        return
        [
            .. from category in categories
               from field in set.Fields
               where !catalog.FieldsOf(category).Any(text => string.Equals(text.PropertyName, field.Id, StringComparison.OrdinalIgnoreCase))
               select $"{declaringType.FullName}: list field '{field.Id}' (in {category.Prefix})",
        ];
    }

    private static IEnumerable<(Type DeclaringType, ListFieldSet Set)> ListFieldSets(IEnumerable<Type> candidates)
    {
        const BindingFlags members = BindingFlags.Public | BindingFlags.Static;

        foreach (Type type in candidates.Where(type => type is { IsAbstract: true, IsSealed: true, IsGenericTypeDefinition: false }))
        {
            IEnumerable<object?> values =
                type.GetProperties(members).Where(property => property.PropertyType == typeof(ListFieldSet)).Select(property => property.GetValue(null))
                    .Concat(type.GetFields(members).Where(field => field.FieldType == typeof(ListFieldSet)).Select(field => field.GetValue(null)));

            foreach (ListFieldSet set in values.OfType<ListFieldSet>())
            {
                yield return (type, set);
            }
        }
    }
}
