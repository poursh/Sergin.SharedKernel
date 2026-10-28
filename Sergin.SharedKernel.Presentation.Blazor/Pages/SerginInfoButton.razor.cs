using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The detail page header's information icon: opens <see cref="SerginInfoDialog"/> with the scope the record
/// belongs to, the row version the page holds, and who created and last changed the record and when.
/// </summary>
/// <remarks>
/// The page passes <see cref="Load"/>, normally a send of its aggregate's audit query, and the version it loaded.
/// The dialog loads when it opens, not with the page: the stamps are what few visits look at, and loading on open
/// also shows what the last save wrote without a page reload.
/// </remarks>
public sealed partial class SerginInfoButton
{
    [Parameter]
    [EditorRequired]
    public Func<Task<ErrorOr<AuditInfo>>> Load { get; set; } = default!;

    [Parameter]
    public RowVersion? Version { get; set; }

    [Inject]
    private IDialogService Dialogs { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    private async Task OpenAsync()
    {
        DialogParameters<SerginInfoDialog> parameters = new()
        {
            { dialog => dialog.Load, Load },
            { dialog => dialog.Version, Version },
        };

        await Dialogs.ShowAsync<SerginInfoDialog>(
            Localizer["Information"].Value,
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true, CloseButton = true });
    }
}
