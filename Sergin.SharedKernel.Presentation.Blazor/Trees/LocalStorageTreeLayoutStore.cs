using System.Text.Json;
using Microsoft.JSInterop;

namespace Sergin.SharedKernel.Presentation.Blazor.Trees;

/// <summary>
/// Stores a tree page's layout in the browser's <c>localStorage</c> under <c>sergin.tree.{pageKey}</c>, straight
/// through <see cref="IJSRuntime"/> the way <see cref="Security.LocalStorageScopeStore"/> does.
/// </summary>
internal sealed class LocalStorageTreeLayoutStore(IJSRuntime jsRuntime) : IUiTreeLayoutStore
{
    private const string KeyPrefix = "sergin.tree.";

    public async ValueTask<SerginTreeLayout> LoadAsync(string pageKey, CancellationToken cancellationToken = default)
    {
        string? json;

        try
        {
            json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, KeyPrefix + pageKey);
        }
        catch (JSException)
        {
            // Storage unreachable. The page's defaults apply.
            return SerginTreeLayout.Default;
        }
        catch (JSDisconnectedException)
        {
            // Circuit already torn down; there is nobody left to render the result.
            return SerginTreeLayout.Default;
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return SerginTreeLayout.Default;
        }

        try
        {
            return JsonSerializer.Deserialize<SerginTreeLayout>(json) ?? SerginTreeLayout.Default;
        }
        catch (JsonException)
        {
            // Not something this store wrote. The next change overwrites it.
            return SerginTreeLayout.Default;
        }
    }

    public async ValueTask SaveAsync(string pageKey, SerginTreeLayout layout, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(layout);

        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, KeyPrefix + pageKey, json);
        }
        catch (JSException)
        {
            // Storage refused the write. The layout still applies for the rest of this circuit.
        }
        catch (JSDisconnectedException)
        {
            // Circuit torn down mid-save; nothing to persist to.
        }
    }
}
