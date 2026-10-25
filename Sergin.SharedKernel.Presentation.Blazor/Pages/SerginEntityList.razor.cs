using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The list body of a <see cref="SerginEntityListSection"/>: a server-paged <c>MudTable</c> of the entities inside one
/// aggregate, with a quick search, optional sortable headers and a column of per-row commands.
/// </summary>
/// <remarks>
/// The page or component supplies <see cref="Load"/>, which builds its feature's list record from a
/// <see cref="SerginListRequest"/> — paging, the search as <c>Term</c>, and sorting when a header carries a
/// <c>MudTableSortLabel</c>; never a filter, since this list has no filter panel. Each load reports the total through
/// <see cref="TotalChanged"/> when it changes, so the owner can show it in the section title.
/// <see cref="BulkActions"/> adds a checkbox column and a <see cref="SerginSelectionBar"/>, as on the aggregate list;
/// every load clears the selection.
/// </remarks>
public sealed partial class SerginEntityList<TItem>
{
    private MudTable<TItem> table = default!;
    private int? total;
    private string? term;
    private bool loading;
    private HashSet<TItem> selected = [];
    private bool bulkRunning;

    [Parameter]
    [EditorRequired]
    public Func<SerginListRequest, CancellationToken, Task<ErrorOr<ListQueryResponse<TItem>>>> Load { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public RenderFragment HeaderContent { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public RenderFragment<TItem> RowTemplate { get; set; } = default!;

    [Parameter]
    public RenderFragment<TItem>? RowCommands { get; set; }

    /// <summary>Raised after a load whose total differs from the last one.</summary>
    [Parameter]
    public EventCallback<int> TotalChanged { get; set; }

    /// <summary>The text shown when the collection is empty, localised by the list.</summary>
    [Parameter]
    public string EmptyText { get; set; } = "Nothing here yet.";

    /// <summary>Whether to show the quick search; false for a list whose query repository does not read <c>Term</c>.</summary>
    [Parameter]
    public bool Searchable { get; set; } = true;

    /// <summary>The search box's placeholder, localised by the list.</summary>
    [Parameter]
    public string SearchPlaceholder { get; set; } = "Quick search…";

    [Parameter]
    public int RowsPerPage { get; set; } = 10;

    [Parameter]
    public bool Dense { get; set; } = true;

    /// <summary>The selection bar's actions; any one turns on the checkbox column.</summary>
    [Parameter]
    public IReadOnlyList<SerginBulkAction<TItem>> BulkActions { get; set; } = [];

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    /// <summary>Loads the current page again — after a row command, or when the owning page reloads.</summary>
    public Task ReloadAsync() => table.ReloadServerData();

    private bool Selectable => BulkActions.Count > 0;

    // Rebuilt on each render, so a running action disables every button of the bar.
    private IReadOnlyList<SerginPageAction> SelectionActions =>
    [
        .. BulkActions.Select(action => new SerginPageAction(
            action.Label,
            action.Icon,
            action.Kind,
            OnClick: EventCallback.Factory.Create(this, () => RunBulkAsync(action)),
            Permission: action.Permission,
            AnyScope: action.AnyScope,
            Disabled: bulkRunning)),
    ];

    private async Task<TableData<TItem>> LoadAsync(TableState state, CancellationToken cancellationToken)
    {
        // MudBlazor's TableState.Page is 0-based; PageIndex is 1-based.
        SerginListRequest request = new(
            Paggination.Create(state.PageSize, state.Page + 1),
            string.IsNullOrWhiteSpace(term) ? null : new Term(term.Trim()),
            Filtering: null,
            state.SortDirection == SortDirection.None ? null : SerginListQueryParts.ToSorting(state.SortLabel, state.SortDirection == SortDirection.Descending));

        // A selection holds rows of the page on screen; any other page, criterion or reload starts without one.
        selected = [];

        // Shown for the first load and for every page, search and sort change: all of them come through here.
        loading = true;
        StateHasChanged();

        try
        {
            ErrorOr<ListQueryResponse<TItem>> result = await Load(request, cancellationToken);

            if (result.IsError)
            {
                ErrorPresenter.Notify(result.FirstError);
                return new TableData<TItem> { Items = [], TotalItems = 0 };
            }

            if (total != result.Value.Total)
            {
                total = result.Value.Total;
                await TotalChanged.InvokeAsync(result.Value.Total);
            }

            return new TableData<TItem> { Items = result.Value.Data, TotalItems = result.Value.Total };
        }
        finally
        {
            loading = false;
        }
    }

    // A new search starts again at the first page; NavigateTo reloads on its own.
    private Task OnTermChangedAsync(string? value)
    {
        term = value;

        if (table.CurrentPage != 0)
        {
            table.NavigateTo(0);
            return Task.CompletedTask;
        }

        return table.ReloadServerData();
    }

    private void OnSelectionChanged(HashSet<TItem> items) => selected = items;

    private void ClearSelection() => selected = [];

    private async Task RunBulkAsync(SerginBulkAction<TItem> action)
    {
        IReadOnlyList<TItem> items = [.. selected];
        bulkRunning = true;

        try
        {
            await action.OnRun(items);
        }
        finally
        {
            bulkRunning = false;
        }
    }
}
