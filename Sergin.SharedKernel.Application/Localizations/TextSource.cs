using System.Reflection;
using System.Runtime.CompilerServices;
using Sergin.SharedKernel.Application.Localizations.Common;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Application.Localizations;

/// <summary>
/// The texts one module declares, found by scanning its types: every public static field or property of type
/// <see cref="TextDefinition"/>, <see cref="FieldText"/> (and the three definitions of each field) or
/// <see cref="ErrorTextPair"/> (its title and detail) on a public
/// static class, nested ones included. <see cref="TextCatalog"/> checks each against the source's module segment.
/// Every source states its <see cref="TextOrigin"/>, which each of its entries carries.
/// </summary>
public sealed class TextSource
{
    private const string CommonModule = "common";

    private TextSource(string moduleSegment, TextOrigin origin, IReadOnlyList<TextCatalogEntry> entries, IReadOnlyList<FieldText> fields)
    {
        ModuleSegment = moduleSegment;
        Origin = origin;
        Entries = entries;
        Fields = fields;
    }

    /// <summary>SharedKernel's own text classes; module segment <c>common</c>.</summary>
    public static TextSource Common { get; } = FromAssembly(typeof(TextSource).Assembly, CommonModule, TextOrigin.Application);

    /// <summary>The module segment keys of this source must start with (kebab case).</summary>
    public string ModuleSegment { get; }

    /// <summary>Whether this source's texts belong to the application or to the UI.</summary>
    public TextOrigin Origin { get; }

    internal IReadOnlyList<TextCatalogEntry> Entries { get; }

    internal IReadOnlyList<FieldText> Fields { get; }

    public static TextSource FromAssembly(Assembly assembly, string moduleName, TextOrigin origin)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return FromTypes(moduleName, origin, assembly.GetTypes());
    }

    /// <summary>
    /// A source whose texts are built at runtime rather than declared as static members, e.g. a third party's resource
    /// keys. <paramref name="origin"/> names where they come from in the catalog's refusals. The same module-segment
    /// and duplicate-key checks apply as for a scanned source. <paramref name="textOrigin"/> is the origin every
    /// entry carries.
    /// </summary>
    public static TextSource FromDefinitions(string moduleName, IEnumerable<TextDefinition> definitions, Type origin, TextOrigin textOrigin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(origin);

        return new TextSource(
            SerginStringKey.ToSegment(moduleName),
            textOrigin,
            [.. definitions.Select(definition => EntryOf(definition, [.. definition.Key.Segments.Skip(1).SkipLast(1)], origin, textOrigin))],
            []);
    }

    /// <summary>
    /// The permission labels of <paramref name="catalog"/> as declared texts, one source per module the labels belong
    /// to, each entry naming its aggregate root as where it came from. A label keyed by the permission name (a test
    /// source's, which names no module) is left out. The labels are declared with the application's permission
    /// definitions, so their origin is <see cref="TextOrigin.Application"/>.
    /// </summary>
    public static IReadOnlyList<TextSource> FromPermissions(PermissionCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return
        [
            .. catalog.Entries
                .Where(entry => entry.InTextCatalog)
                .GroupBy(entry => entry.Label.Key.Module, StringComparer.Ordinal)
                .Select(module => new TextSource(
                    module.Key,
                    TextOrigin.Application,
                    [
                        .. module.Select(entry => EntryOf(
                            entry.Label,
                            [.. entry.Label.Key.Segments.Skip(1).SkipLast(1)],
                            entry.AggregateRoot ?? typeof(PermissionCatalog),
                            TextOrigin.Application)),
                    ],
                    [])),
        ];
    }

    public static TextSource FromTypes(string moduleName, TextOrigin origin, params Type[] declaringTypes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(moduleName);
        ArgumentNullException.ThrowIfNull(declaringTypes);

        string segment = SerginStringKey.ToSegment(moduleName);
        List<TextCatalogEntry> entries = [];
        List<FieldText> fields = [];

        foreach (Type type in declaringTypes.Where(IsDeclaringType))
        {
            Collect(type, origin, entries, fields);
        }

        return new TextSource(segment, origin, entries, fields);
    }

    private static bool IsDeclaringType(Type type) =>
        type.IsAbstract && type.IsSealed && (type.IsPublic || type.IsNestedPublic) && !type.IsGenericTypeDefinition;

    private static void Collect(Type type, TextOrigin origin, List<TextCatalogEntry> entries, List<FieldText> fields)
    {
        try
        {
            RuntimeHelpers.RunClassConstructor(type.TypeHandle);

            foreach (object? value in MembersOf(type))
            {
                switch (value)
                {
                    case TextDefinition definition:
                        entries.Add(EntryOf(definition, [.. definition.Key.Segments.Skip(1).SkipLast(1)], type, origin));
                        break;
                    case FieldText field:
                        fields.Add(field);
                        foreach (TextDefinition? part in new[] { field.Label, field.Hint, field.Placeholder })
                        {
                            if (part is not null)
                            {
                                entries.Add(EntryOf(part, field.Category.Path, type, origin));
                            }
                        }

                        break;
                    case ErrorTextPair pair:
                        entries.Add(EntryOf(pair.Title, [.. pair.Title.Key.Segments.Skip(1).SkipLast(1)], type, origin));
                        entries.Add(EntryOf(pair.Detail, [.. pair.Detail.Key.Segments.Skip(1).SkipLast(1)], type, origin));
                        break;
                    default:
                        break;
                }
            }
        }
        catch (Exception error) when (error is TypeInitializationException or TargetInvocationException)
        {
            throw new InvalidOperationException(
                $"{type.FullName} declares a text that cannot be built: {(error.InnerException ?? error).Message}", error);
        }
    }

    private static IEnumerable<object?> MembersOf(Type type)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (FieldInfo field in type.GetFields(flags).Where(f => IsText(f.FieldType)))
        {
            yield return field.GetValue(null);
        }

        foreach (PropertyInfo property in type.GetProperties(flags).Where(p => IsText(p.PropertyType) && p.GetIndexParameters().Length == 0))
        {
            yield return property.GetValue(null);
        }
    }

    private static bool IsText(Type type) =>
        type == typeof(TextDefinition) || type == typeof(FieldText) || type == typeof(ErrorTextPair);

    private static TextCatalogEntry EntryOf(
        TextDefinition definition, IReadOnlyList<string> category, Type declaringType, TextOrigin origin) =>
        new(definition, definition.Key.Module, category, declaringType, origin);
}
