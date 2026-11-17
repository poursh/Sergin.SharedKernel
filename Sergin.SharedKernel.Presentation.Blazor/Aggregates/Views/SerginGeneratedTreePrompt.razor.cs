using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The prompt a tree's own page shows while no node is selected: the tree's prompt icon, title and optional hint.
/// Rendering a root with no tree, or no list or detail route, throws.
/// </summary>
public sealed partial class SerginGeneratedTreePrompt<TRoot>
    where TRoot : class, IAggregateRoot
{
    private TreeView tree = default!;

    [Inject]
    private AggregateViewRegistry Views { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    protected override void OnInitialized() => tree = GeneratedTreeParts.For(Views.For<TRoot>()).Tree;
}
