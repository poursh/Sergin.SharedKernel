using System.Globalization;
using Microsoft.AspNetCore.Components;
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
    private const int IndentStepPixels = 16;

    private bool open;
    private string? search;

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

    /// <summary>
    /// The listed scopes whose name matches the search, with their listed ancestors so each match keeps its place
    /// in the tree.
    /// </summary>
    private IReadOnlyList<ScopeOption> Shown
    {
        get
        {
            if (string.IsNullOrWhiteSpace(search))
            {
                return Session.Options;
            }

            string term = search.Trim();

            HashSet<Guid> kept =
            [
                .. Session.Options
                    .Where(option => option.Name.Contains(term, StringComparison.CurrentCultureIgnoreCase))
                    .SelectMany(Session.TrailOf)
                    .Select(option => option.Id)
            ];

            return [.. Session.Options.Where(option => kept.Contains(option.Id))];
        }
    }

    private string IndentOf(ScopeOption option) =>
        string.Create(CultureInfo.InvariantCulture, $"padding-inline-start: {(Session.TrailOf(option).Count - 1) * IndentStepPixels}px;");

    private async Task ToggleAsync()
    {
        if (open)
        {
            open = false;
            return;
        }

        search = null;

        if (await Session.RefreshAsync())
        {
            await ScopeChanged.InvokeAsync();
        }

        open = true;
    }

    private async Task SelectAsync(Guid scopeId)
    {
        open = false;

        if (await Session.SelectAsync(scopeId))
        {
            await ScopeChanged.InvokeAsync();
        }
    }
}
