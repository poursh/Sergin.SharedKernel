using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Presentation.Blazor.Security;

/// <summary>
/// Shows the scope the circuit works in and lets the user move to any scope <see cref="IScopeDirectory"/> lists for
/// them, by name.
/// </summary>
/// <remarks>
/// A view over <see cref="SerginScopeSession"/>, which the layout initialises before the page renders. The list is
/// reloaded every time the picker opens, so a grant change shows without a reload. <see cref="ScopeChanged"/> lets
/// the layout rebuild what it rendered on the old scope.
/// </remarks>
public sealed partial class SerginScopePicker
{
    private const string TrailSeparator = " › ";

    [Inject]
    private IDialogService Dialogs { get; set; } = default!;

    [Inject]
    private SerginScopeSession Session { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    /// <summary>
    /// Raised after the scope changed.
    /// </summary>
    [Parameter]
    public EventCallback ScopeChanged { get; set; }

    private ScopeOption? Current => Session.Current;

    private string CurrentName => Current?.Name ?? Localizer["No scope"].Value;

    private string Trail => Current is null
        ? Localizer["You have no scope to work in."].Value
        : string.Join(TrailSeparator, Session.TrailOf(Current).Select(option => option.Name));

    /// <summary>
    /// Composed here rather than through the localizer's arguments, which <c>DefaultLocalizer</c> ignores.
    /// </summary>
    private string AriaLabel => $"{Localizer["Scope"].Value}: {CurrentName}";

    private async Task OpenAsync()
    {
        if (await Session.RefreshAsync())
        {
            await ScopeChanged.InvokeAsync();
        }

        IDialogReference dialog = await Dialogs.ShowAsync<SerginScopePickerDialog>(
            Localizer["Scope"].Value,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true });

        DialogResult? result = await dialog.Result;

        if (result is { Canceled: false, Data: Guid scopeId } && await Session.SelectAsync(scopeId))
        {
            await ScopeChanged.InvokeAsync();
        }
    }
}
