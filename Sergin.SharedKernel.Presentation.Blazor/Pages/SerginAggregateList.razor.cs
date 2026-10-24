using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Errors;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// An aggregate's list body: a server-paged <c>MudTable</c> with quick search, sortable headers and an optional
/// advanced filter, all sent to the query rather than applied to the page of rows already loaded.
/// </summary>
/// <remarks>
/// <para>
/// The page supplies <see cref="Load"/>, which builds its feature's list record from a <see cref="SerginListRequest"/>
/// and sends it; <see cref="HeaderContent"/> with a <c>MudTableSortLabel</c> per sortable column, its
/// <c>SortLabel</c> being the field id of the feature's <c>ListFieldSet</c>; and <see cref="RowTemplate"/>.
/// <see cref="RowCommands"/> adds a right-aligned column of per-row icon commands.
/// </para>
/// <para>
/// The filter panel's fields belong to the page, bound to its own model. Apply asks the page for the filters that
/// model makes (<see cref="BuildFilters"/>) and shows each as a chip; removing a chip tells the page
/// (<see cref="OnFilterRemoved"/>) so it can clear the field, and Reset clears every one (<see cref="OnFiltersReset"/>).
/// </para>
/// </remarks>
public sealed partial class SerginAggregateList<TItem>
{
    private MudTable<TItem> table = default!;
    private IReadOnlyList<SerginAppliedFilter> filters = [];
    private string? term;
    private bool filterPanelOpen;

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

    /// <summary>The advanced filter's fields; null for a list with no advanced filter.</summary>
    [Parameter]
    public RenderFragment? FilterPanel { get; set; }

    [Parameter]
    public Func<IReadOnlyList<SerginAppliedFilter>>? BuildFilters { get; set; }

    [Parameter]
    public EventCallback<SerginAppliedFilter> OnFilterRemoved { get; set; }

    [Parameter]
    public EventCallback OnFiltersReset { get; set; }

    /// <summary>Raised each time the filter panel opens, so the page can load its pickers' options only when needed.</summary>
    [Parameter]
    public EventCallback OnFilterPanelOpened { get; set; }

    /// <summary>Whether to show the quick search; false for a list whose query repository does not read <c>Term</c>.</summary>
    [Parameter]
    public bool Searchable { get; set; } = true;

    [Parameter]
    public string SearchPlaceholder { get; set; } = "Quick search…";

    [Parameter]
    public int RowsPerPage { get; set; } = 10;

    [Parameter]
    public bool Dense { get; set; }

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    private bool HasCriteria => filters.Count > 0 || !string.IsNullOrWhiteSpace(term);

    /// <summary>Loads the current page again with the current criteria — the header's reload icon, or after a row command.</summary>
    public Task ReloadAsync() => table.ReloadServerData();

    private async Task<TableData<TItem>> LoadAsync(TableState state, CancellationToken cancellationToken)
    {
        // MudBlazor's TableState.Page is 0-based; PageIndex is 1-based.
        SerginListRequest request = new(
            Paggination.Create(state.PageSize, state.Page + 1),
            string.IsNullOrWhiteSpace(term) ? null : new Term(term.Trim()),
            SerginListQueryParts.ToFiltering(filters),
            state.SortDirection == SortDirection.None ? null : SerginListQueryParts.ToSorting(state.SortLabel, state.SortDirection == SortDirection.Descending));

        ErrorOr<ListQueryResponse<TItem>> result = await Load(request, cancellationToken);

        if (result.IsError)
        {
            ErrorPresenter.Notify(result.FirstError);
            return new TableData<TItem> { Items = [], TotalItems = 0 };
        }

        return new TableData<TItem> { Items = result.Value.Data, TotalItems = result.Value.Total };
    }

    private Task OnTermChangedAsync(string? value)
    {
        term = value;
        return FirstPageAsync();
    }

    private Task ClearTermAsync()
    {
        term = null;
        return FirstPageAsync();
    }

    private async Task ToggleFilterPanel()
    {
        filterPanelOpen = !filterPanelOpen;

        if (filterPanelOpen)
        {
            await OnFilterPanelOpened.InvokeAsync();
        }
    }

    private Task ApplyFiltersAsync()
    {
        filters = BuildFilters?.Invoke() ?? [];
        return FirstPageAsync();
    }

    private async Task ResetFiltersAsync()
    {
        filters = [];
        await OnFiltersReset.InvokeAsync();
        await FirstPageAsync();
    }

    private async Task RemoveFilterAsync(SerginAppliedFilter filter)
    {
        filters = [.. filters.Where(applied => applied != filter)];
        await OnFilterRemoved.InvokeAsync(filter);
        await FirstPageAsync();
    }

    private async Task ClearAllAsync()
    {
        term = null;
        await ResetFiltersAsync();
    }

    // New criteria start again at the first page; staying on page 4 of a result that now has one page shows nothing.
    private Task FirstPageAsync()
    {
        if (table.CurrentPage != 0)
        {
            table.NavigateTo(0);
            return Task.CompletedTask;
        }

        return table.ReloadServerData();
    }
}
