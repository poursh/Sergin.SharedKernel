using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The selection bar of <see cref="SerginAggregateList{TItem}"/> and <see cref="SerginEntityList{TItem}"/>: how many
/// rows are selected, the bulk actions (through <see cref="SerginActionBar"/>, so a forbidden one is hidden), and a
/// Clear button.
/// </summary>
public sealed partial class SerginSelectionBar
{
    [Parameter]
    public int Count { get; set; }

    [Parameter]
    public IReadOnlyList<SerginPageAction> Actions { get; set; } = [];

    [Parameter]
    public EventCallback OnClear { get; set; }

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;
}
