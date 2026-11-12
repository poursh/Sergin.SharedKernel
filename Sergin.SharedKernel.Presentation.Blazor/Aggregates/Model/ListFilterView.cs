using Sergin.SharedKernel.Application.Commands.Queries;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// One <see cref="ListFieldKind.Identifier"/> filter of a root's list, and where its options come from: a component the
/// configuration gave with <c>Filter(id, component)</c>, or the target its id names — <c>&lt;name&gt;Id</c> names a
/// root <c>&lt;Name&gt;</c>, or a child entity <c>&lt;Name&gt;</c> filtered under the filter named after its root.
/// </summary>
/// <param name="Field">The list field.</param>
/// <param name="Component">The configured picker component (<c>[Parameter] Guid? Value</c>, <c>ValueChanged</c>), or null.</param>
/// <param name="TargetRoot">The root whose records are offered, or the child's root; null with a component.</param>
/// <param name="TargetChild">The child entity offered, or null for a root.</param>
/// <param name="ParentFilterId">For a child, the id of the filter that picks its root.</param>
/// <param name="Options">Loads the generated picker's options (a child's under the chosen root); null with a component.</param>
public sealed record ListFilterView(
    ListField Field,
    Type? Component,
    Type? TargetRoot,
    Type? TargetChild,
    string? ParentFilterId,
    PickerOptions? Options = null);
