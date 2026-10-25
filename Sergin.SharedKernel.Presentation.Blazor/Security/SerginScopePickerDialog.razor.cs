using System.Globalization;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Presentation.Blazor.Security;

/// <summary>
/// The modal <see cref="SerginScopePicker"/> opens: lists the scopes <see cref="SerginScopeSession"/> holds and
/// closes with the id of the one the user chose, leaving the switch itself to the picker.
/// </summary>
public sealed partial class SerginScopePickerDialog
{
    private const int IndentStepPixels = 16;

    private string? search;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Inject]
    private SerginScopeSession Session { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

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

    private void Select(Guid scopeId) => MudDialog.Close(DialogResult.Ok(scopeId));

    private void Cancel() => MudDialog.Cancel();
}
