using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// A collection of the entities inside the aggregate a detail page shows — a manufacturer's device models — laid out
/// like <see cref="SerginAggregatePage"/> without its overview: a title with the count, the collection's
/// <see cref="Actions"/> bar, then the body (<see cref="ChildContent"/>, usually a <see cref="SerginEntityList{TItem}"/>).
/// </summary>
/// <remarks>
/// A section, not a page: it sits in a detail page's body and declares no route or header. Its actions are the
/// root's behaviours on the collection, so they are guarded by the root's permissions and sent at the root's row
/// version like any other write to the aggregate. The owner sets <see cref="Count"/>, typically from the list's
/// <see cref="SerginEntityList{TItem}.TotalChanged"/>.
/// </remarks>
public sealed partial class SerginEntityListSection
{
    private bool collapsed;

    [Parameter]
    [EditorRequired]
    public string Title { get; set; } = string.Empty;

    /// <summary>The number of entities, shown after the title; null until known.</summary>
    [Parameter]
    public int? Count { get; set; }

    [Parameter]
    public IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

    /// <summary>Whether the section starts collapsed: the title and count only.</summary>
    [Parameter]
    public bool Collapsed { get; set; }

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    protected override void OnInitialized() => collapsed = Collapsed;

    private void Toggle() => collapsed = !collapsed;
}
