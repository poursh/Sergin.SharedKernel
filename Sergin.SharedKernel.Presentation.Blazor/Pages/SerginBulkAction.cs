using Sergin.SharedKernel.Domain.Securities;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// One button of a list's selection bar: what runs on the rows the user selected. Giving a
/// <see cref="SerginAggregateList{TItem}"/> or <see cref="SerginEntityList{TItem}"/> at least one turns on its
/// checkbox column.
/// </summary>
/// <param name="Label">The button text, localised by the bar.</param>
/// <param name="Icon">A MudBlazor icon constant; null for none.</param>
/// <param name="Kind">Its look and place, as in the actions bar.</param>
/// <param name="OnRun">
/// What a click runs, given the selected rows. The owner reloads the list when done, which clears the
/// selection.
/// </param>
/// <param name="Permission">
/// The permission the action needs; the bar hides it from a user who does not hold it — presentation only, as for
/// <see cref="SerginPageAction"/>.
/// </param>
/// <param name="AnyScope">Whether the permission is checked on any scope rather than the current one.</param>
public sealed record SerginBulkAction<TItem>(
    string Label,
    string? Icon,
    SerginActionKind Kind,
    Func<IReadOnlyList<TItem>, Task> OnRun,
    PermissionDefinition? Permission = null,
    bool AnyScope = false);
