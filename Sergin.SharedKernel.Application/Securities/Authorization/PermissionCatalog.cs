using System.Diagnostics.CodeAnalysis;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Localizations.Common;
using Sergin.SharedKernel.Domain.Localizations;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// Every permission the platform knows, as each aggregate registered it with
/// <c>AggregateFeatureBuilder.Permissions(...)</c>, plus <see cref="PermissionDefinition.AllPlatform"/>. Built once
/// from the same sources as the command configuration registry, so a registration mistake fails host start:
/// the same name from two aggregates, a permission outside its module's schema, a module registering the
/// reserved <c>sys</c> schema (only an aggregate SharedKernel owns registers there), or a definition class whose initializer rejected its own name.
/// </summary>
/// <remarks>
/// Each entry carries its label as a declared text (spec decision 19), the definition's own label being the English
/// default: <c>&lt;root's category prefix&gt;.permissions.&lt;last name segment&gt;</c> when the root named a category
/// with <c>Texts(...)</c>; otherwise, for a module's source, <c>&lt;module&gt;.&lt;the name's resource segments&gt;.permissions.&lt;last&gt;</c>
/// (<c>permission.dm.devices.read</c> in DeviceManagement is <c>device-management.devices.permissions.read</c>, the same key
/// the root's category would give); the platform permission's is <c>common.permissions.platform-all</c>. A test source,
/// which names no module, keys the label by the permission name and leaves it out of the text catalog.
/// </remarks>
public sealed class PermissionCatalog
{
    private const string ReservedSchema = "sys";
    private const string PlatformGroup = "Platform";
    private const string PermissionsSegment = "Permissions";

    private readonly IReadOnlyList<PermissionCatalogEntry> entries;
    private readonly Dictionary<string, PermissionDefinition> byName;
    private readonly Dictionary<string, TextDefinition> labels;

    private PermissionCatalog(IReadOnlyList<PermissionCatalogEntry> entries)
    {
        this.entries = entries;
        byName = entries.ToDictionary(entry => entry.Definition.Permission.Value, entry => entry.Definition, StringComparer.Ordinal);
        labels = entries.ToDictionary(entry => entry.Definition.Permission.Value, entry => entry.Label, StringComparer.Ordinal);
    }

    public static PermissionCatalog Empty { get; } =
        new([PlatformEntry()]);

    /// <summary>Every registered permission, <see cref="PermissionDefinition.AllPlatform"/> included.</summary>
    public IReadOnlyCollection<PermissionCatalogEntry> Entries => entries;

    public static PermissionCatalog FromSources(IEnumerable<ModuleConfigurationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        List<PermissionCatalogEntry> collected = [PlatformEntry()];

        foreach (ModuleConfigurationSource source in sources)
        {
            var registry = AggregateFeatureRegistry.FromConfigurationTypes(source.AggregateConfigurationTypes);

            foreach (Type root in registry.ConfiguredTypes)
            {
                foreach (PermissionDefinition definition in registry.PermissionsOf(root))
                {
                    CheckSchema(source.Schema, root, definition);
                    collected.Add(EntryOf(definition, root, registry.TextsOf(root), source.ModuleName) with
                    {
                        GroupTitle = registry.NameOf(root) ?? CommonTexts.PermissionGroupOther,
                    });
                }
            }
        }

        CheckNames(collected);

        // The same definition from the same root twice (two sources) is one entry, preferring the one whose label is a
        // module's text; CheckNames has refused the rest.
        return new PermissionCatalog(
        [
            .. collected
                .OrderBy(entry => entry.InTextCatalog ? 0 : 1)
                .DistinctBy(entry => entry.Definition.Permission.Value, StringComparer.Ordinal),
        ]);
    }

    public bool Contains(PermissionDefinition permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        return byName.ContainsKey(permission.Permission.Value);
    }

    /// <summary>The declared text <paramref name="permission"/>'s label is shown with.</summary>
    public TextDefinition LabelOf(PermissionDefinition permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        return labels.TryGetValue(permission.Permission.Value, out TextDefinition? label)
            ? label
            : throw new ArgumentException($"{permission.Permission.Value} is not in the permission catalogue.", nameof(permission));
    }

    public bool TryGet(Permission permission, [NotNullWhen(true)] out PermissionDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(permission);

        return byName.TryGetValue(permission.Value, out definition);
    }

    /// <summary>
    /// The catalogue by module, then aggregate, then permission, each ordered ordinally. The platform
    /// permission is the <c>sys</c> module's <c>Platform</c> group.
    /// </summary>
    public IReadOnlyList<PermissionCatalogModule> Grouped() =>
    [
        .. entries
            .GroupBy(entry => entry.Definition.Schema, StringComparer.Ordinal)
            .OrderBy(module => module.Key, StringComparer.Ordinal)
            .Select(module => new PermissionCatalogModule(
                module.Key,
                [
                    .. module
                        .GroupBy(entry => entry.AggregateRoot?.Name ?? PlatformGroup, StringComparer.Ordinal)
                        .OrderBy(group => group.Key, StringComparer.Ordinal)
                        .Select(group => new PermissionCatalogGroup(
                            group.Key,
                            group.First().GroupTitle,
                            [
                                .. group
                                    .Select(entry => entry.Definition)
                                    .OrderBy(definition => definition.Permission.Value, StringComparer.Ordinal)
                            ]))
                ]))
    ];

    private static PermissionCatalogEntry PlatformEntry() =>
        new(
            PermissionDefinition.AllPlatform,
            AggregateRoot: null,
            TextCategory.Create("Common", PermissionsSegment).Text(ActionOf(PermissionDefinition.AllPlatform), PermissionDefinition.AllPlatform.Label),
            CommonTexts.PermissionGroupPlatform);

    private static PermissionCatalogEntry EntryOf(PermissionDefinition definition, Type root, TextCategory? category, string? moduleName)
    {
        string action = ActionOf(definition);

        if (category is not null)
        {
            return new(definition, root, category.Child(PermissionsSegment).Text(action, definition.Label), CommonTexts.PermissionGroupOther);
        }

        if (moduleName is not null)
        {
            string[] resource = [.. definition.Permission.Segments.Skip(2).SkipLast(1), PermissionsSegment];

            return new(definition, root, TextCategory.Create(moduleName, resource).Text(action, definition.Label), CommonTexts.PermissionGroupOther);
        }

        return new(definition, root, new TextDefinition(TextKey.Create(definition.Permission.Value), definition.Label), CommonTexts.PermissionGroupOther)
        {
            InTextCatalog = false,
        };
    }

    private static string ActionOf(PermissionDefinition definition) => definition.Permission.Segments[^1];

    private static void CheckSchema(string? schema, Type root, PermissionDefinition definition)
    {
        if (definition.Schema == ReservedSchema && !IsPlatformRoot(root))
        {
            throw new InvalidOperationException(
                $"{root.FullName} registers {definition.Permission.Value}, but the '{ReservedSchema}' schema is reserved "
                + "for the platform's own permissions; a module registers permissions in its own schema.");
        }

        if (schema is not null && definition.Schema != schema)
        {
            throw new InvalidOperationException(
                $"{root.FullName} registers {definition.Permission.Value}, which is outside its module's schema: "
                + $"expected permission.{schema}.*.");
        }
    }

    /// <summary>
    /// An aggregate SharedKernel itself owns (the scheduled task), whose permission no one module can own and so lives
    /// in <c>sys</c>; a module's aggregate, or a test's, never does.
    /// </summary>
    private static bool IsPlatformRoot(Type root) =>
        root.Assembly.GetName().Name?.StartsWith("Sergin.SharedKernel.", StringComparison.Ordinal) == true;

    private static void CheckNames(List<PermissionCatalogEntry> collected)
    {
        foreach (IGrouping<string, PermissionCatalogEntry> group in collected.GroupBy(entry => entry.Definition.Permission.Value, StringComparer.Ordinal))
        {
            PermissionCatalogEntry[] entries = [.. group];

            if (entries.Select(entry => entry.AggregateRoot).Distinct().Count() > 1)
            {
                throw new InvalidOperationException(
                    $"{group.Key} is registered by more than one aggregate: "
                    + $"{string.Join(", ", entries.Select(entry => entry.AggregateRoot?.FullName ?? PlatformGroup).Distinct())}. "
                    + "A permission belongs to exactly one aggregate.");
            }

            if (entries.Select(entry => entry.Definition.Label).Distinct(StringComparer.Ordinal).Count() > 1)
            {
                throw new InvalidOperationException(
                    $"{group.Key} is registered by {entries[0].AggregateRoot?.FullName ?? PlatformGroup} with more than one label: "
                    + $"{string.Join(", ", entries.Select(entry => $"'{entry.Definition.Label}'").Distinct())}.");
            }
        }
    }
}
