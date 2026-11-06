using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>The one delete confirmation every page shows — a row command and a detail page alike.</summary>
public static class SerginDialogExtensions
{
    /// <summary>
    /// Asks before a delete; true only for an explicit yes. <paramref name="title"/> names what is deleted
    /// (<c>Delete device</c>), <paramref name="message"/> what happens (<c>Delete MTR-000142? It disappears from
    /// every list and page.</c>).
    /// </summary>
    public static async Task<bool> ConfirmDeleteAsync(
        this IDialogService dialogs,
        IStringLocalizer localizer,
        TextDefinition title,
        TextDefinition message,
        object? arguments = null,
        TextDefinition? confirmText = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(message);

        bool? confirmed = await dialogs.ShowMessageBoxAsync(
            localizer.Text(title, arguments),
            localizer.Text(message, arguments),
            yesText: localizer.Text(confirmText ?? CommonTexts.Delete),
            cancelText: localizer.Text(CommonTexts.Cancel));

        return confirmed == true;
    }

    /// <summary>
    /// Opens <see cref="SerginInfoDialog"/>: the record's scope, the row version the caller holds and its audit
    /// stamps, loaded through <paramref name="load"/> when the dialog opens.
    /// </summary>
    public static async Task ShowInfoAsync(this IDialogService dialogs, string title, Func<Task<ErrorOr<AuditInfo>>> load, RowVersion? version = null)
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        DialogParameters<SerginInfoDialog> parameters = new()
        {
            { dialog => dialog.Load, load },
            { dialog => dialog.Version, version },
        };

        await dialogs.ShowAsync<SerginInfoDialog>(
            title,
            parameters,
            new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true, CloseButton = true });
    }
}
