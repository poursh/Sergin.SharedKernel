using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using MudBlazor;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Pages;

/// <summary>
/// The page header's copy-link icon: puts the current URL on the clipboard. Only ever runs from a click, so the
/// circuit is interactive and interop is available; a browser that refuses the clipboard gets a warning instead.
/// </summary>
public sealed partial class SerginCopyLinkButton
{
    [Inject]
    private IJSRuntime JS { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ISnackbar Snackbar { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    private async Task CopyAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", Navigation.Uri);
            Snackbar.Add(Localizer["Link copied"], Severity.Normal);
        }
        catch (JSException)
        {
            Snackbar.Add(Localizer["The browser did not allow copying the link."], Severity.Warning);
        }
    }
}
