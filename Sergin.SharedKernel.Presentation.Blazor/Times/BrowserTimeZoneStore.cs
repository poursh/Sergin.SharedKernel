using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace Sergin.SharedKernel.Presentation.Blazor.Times;

/// <summary>
/// Reads the browser's own timezone via <c>Intl.DateTimeFormat().resolvedOptions().timeZone</c>,
/// straight through <see cref="IJSRuntime"/> rather than shipping a helper <c>.js</c> file — the same
/// reasoning as <c>LocalStorageThemeStore</c>, and it keeps this RCL free of static web assets.
/// </summary>
internal sealed class BrowserTimeZoneStore(IJSRuntime jsRuntime, IOptions<SerginTimeZoneOptions> options) : IUiTimeZoneStore
{
    public async ValueTask<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default)
    {
        string? browserId;

        try
        {
            browserId = await jsRuntime.InvokeAsync<string?>(
                "eval", cancellationToken, "Intl.DateTimeFormat().resolvedOptions().timeZone");
        }
        catch (JSException)
        {
            // Interop unavailable (still prerendering) or the call failed. Fall back to the app default.
            return AppDefaultTimeZone();
        }
        catch (JSDisconnectedException)
        {
            // Circuit already torn down; there is nobody left to render the result.
            return AppDefaultTimeZone();
        }

        if (string.IsNullOrWhiteSpace(browserId))
        {
            return AppDefaultTimeZone();
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(browserId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // The browser reported an id this host's ICU/tzdata doesn't recognize. Fall back rather
            // than faulting the render.
            return AppDefaultTimeZone();
        }
    }

    private TimeZoneInfo AppDefaultTimeZone() => TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
}
