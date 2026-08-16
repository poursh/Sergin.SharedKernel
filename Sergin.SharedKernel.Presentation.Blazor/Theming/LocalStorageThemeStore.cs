using System.Text.Json;
using Microsoft.JSInterop;

namespace Sergin.SharedKernel.Presentation.Blazor.Theming;

/// <summary>
/// Stores the appearance in the browser's <c>localStorage</c>.
/// </summary>
/// <remarks>
/// Calls <c>localStorage.getItem</c>/<c>setItem</c>/<c>removeItem</c> straight through
/// <see cref="IJSRuntime"/> rather than shipping a helper <c>.js</c> file, which keeps this RCL free
/// of static web assets — one less thing to thread through the host's asset chain.
/// </remarks>
internal sealed class LocalStorageThemeStore(IJSRuntime jsRuntime) : IUiThemeStore
{
    private const string StorageKey = "sergin.ui.theme";

    public async ValueTask<StoredUiTheme?> LoadAsync(CancellationToken cancellationToken = default)
    {
        string? json;

        try
        {
            json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, StorageKey);
        }
        catch (JSException)
        {
            // Storage unreachable. Fall back to the shell's defaults.
            return null;
        }
        catch (JSDisconnectedException)
        {
            // Circuit already torn down; there is nobody left to render the result.
            return null;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<StoredUiTheme>(json);
        }
        catch (JsonException)
        {
            // Written against an older MudBlazor theme shape. Discard it rather than trapping the
            // user in a shell that fails on every load.
            await ClearAsync(cancellationToken);

            return null;
        }
    }

    public async ValueTask SaveAsync(StoredUiTheme theme, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(theme);

        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey, json);
        }
        catch (JSException)
        {
            // Storage refused the write. The theme still applies for the rest of this circuit.
        }
        catch (JSDisconnectedException)
        {
            // Circuit torn down mid-save; nothing to persist to.
        }
    }

    public async ValueTask ClearAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.removeItem", cancellationToken, StorageKey);
        }
        catch (JSException)
        {
            // Nothing to do — the stored value is already unreachable.
        }
        catch (JSDisconnectedException)
        {
            // Circuit torn down; the browser keeps whatever it has.
        }
    }
}
