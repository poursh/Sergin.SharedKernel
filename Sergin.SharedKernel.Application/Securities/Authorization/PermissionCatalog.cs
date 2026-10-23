using System.Diagnostics.CodeAnalysis;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// Every permission the platform knows, as each aggregate registered it with
/// <c>AggregateFeatureBuilder.Permissions(...)</c>, plus <see cref="PermissionDefinition.AllPlatform"/>. Built once
/// from the same sources as the command configuration registry, so a registration mistake fails host start:
/// the same name from two aggregates, a permission outside its module's schema, a module registering the
/// reserved <c>sys</c> schema, or a definition class whose initializer rejected its own name.
/// </summary>
public sealed class PermissionCatalog
{
    private const string ReservedSchema = "sys";
    private const string PlatformGroup = "Platform";

    private readonly IReadOnlyList<PermissionCatalogEntry> entries;
    private readonly Dictionary<string, PermissionDefinition> byName;

    private PermissionCatalog(IReadOnlyList<PermissionCatalogEntry> entries)
    {
        this.entries = entries;
        byName = entries.ToDictionary(entry => entry.Definition.Permission.Value, entry => entry.Definition, StringComparer.Ordinal);
    }

    public static PermissionCatalog Empty { get; } =
        new([new PermissionCatalogEntry(PermissionDefinition.AllPlatform, AggregateRoot: null)]);

    /// <summary>Every registered permission, <see cref="PermissionDefinition.AllPlatform"/> included.</summary>
    public IReadOnlyCollection<PermissionCatalogEntry> Entries => entries;

    public static PermissionCatalog FromSources(IEnumerable<ModuleConfigurationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        List<PermissionCatalogEntry> collected = [new(PermissionDefinition.AllPlatform, AggregateRoot: null)];

        foreach (ModuleConfigurationSource source in sources)
        {
            var registry = AggregateFeatureRegistry.FromConfigurationTypes(source.AggregateConfigurationTypes);

            foreach (Type root in registry.ConfiguredTypes)
            {
                foreach (PermissionDefinition definition in registry.PermissionsOf(root))
                {
                    CheckSchema(source.Schema, root, definition);
                    collected.Add(new PermissionCatalogEntry(definition, root));
                }
            }
        }

        CheckNames(collected);

        // The same definition from the same root twice (two sources) is one entry; CheckNames has refused the rest.
        return new PermissionCatalog([.. collected.DistinctBy(entry => entry.Definition.Permission.Value, StringComparer.Ordinal)]);
    }

    public bool Contains(PermissionDefinition permission)
    {
        ArgumentNullException.ThrowIfNull(permission);

        return byName.ContainsKey(permission.Permission.Value);
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
                            [
                                .. group
                                    .Select(entry => entry.Definition)
                                    .OrderBy(definition => definition.Permission.Value, StringComparer.Ordinal)
                            ]))
                ]))
    ];

    private static void CheckSchema(string? schema, Type root, PermissionDefinition definition)
    {
        if (definition.Schema == ReservedSchema)
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
