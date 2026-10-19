using System.Text.Json;
using Microsoft.JSInterop;

namespace Sergin.SharedKernel.Presentation.Blazor.Security;

/// <summary>
/// Stores the picked scope in the browser's <c>localStorage</c>, straight through <see cref="IJSRuntime"/> the way
/// <see cref="Theming.LocalStorageThemeStore"/> does.
/// </summary>
internal sealed class LocalStorageScopeStore(IJSRuntime jsRuntime) : IUiScopeStore
{
    private const string StorageKey = "sergin.ui.scope";

    public async ValueTask<Guid?> LoadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        string? json;

        try
        {
            json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", cancellationToken, StorageKey);
        }
        catch (JSException)
        {
            // Storage unreachable. Fall back to the home scope.
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

        StoredScope? stored;

        try
        {
            stored = JsonSerializer.Deserialize<StoredScope>(json);
        }
        catch (JsonException)
        {
            // Not something this store wrote. The next pick overwrites it.
            return null;
        }

        return stored is not null && stored.UserId == userId ? stored.ScopeId : null;
    }

    public async ValueTask SaveAsync(Guid userId, Guid scopeId, CancellationToken cancellationToken = default)
    {
        string json = JsonSerializer.Serialize(new StoredScope(userId, scopeId));

        try
        {
            await jsRuntime.InvokeVoidAsync("localStorage.setItem", cancellationToken, StorageKey, json);
        }
        catch (JSException)
        {
            // Storage refused the write. The scope still applies for the rest of this circuit.
        }
        catch (JSDisconnectedException)
        {
            // Circuit torn down mid-save; nothing to persist to.
        }
    }

    private sealed record StoredScope(Guid UserId, Guid ScopeId);
}
