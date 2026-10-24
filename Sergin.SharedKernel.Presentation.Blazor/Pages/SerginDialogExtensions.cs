using MudBlazor;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>The one delete confirmation every page shows — a row command and a detail page alike.</summary>
public static class SerginDialogExtensions
{
    /// <summary>
    /// Asks before a delete; true only for an explicit yes. <paramref name="title"/> names what is deleted
    /// (<c>Delete device</c>), <paramref name="message"/> what happens (<c>Delete MTR-000142? It disappears from
    /// every list and page.</c>).
    /// </summary>
    public static async Task<bool> ConfirmDeleteAsync(this IDialogService dialogs, string title, string message, string confirmText = "Delete")
    {
        ArgumentNullException.ThrowIfNull(dialogs);

        bool? confirmed = await dialogs.ShowMessageBoxAsync(title, message, yesText: confirmText, cancelText: "Cancel");

        return confirmed == true;
    }
}
