namespace Sergin.SharedKernel.Presentation.Blazor.Times;

/// <summary>
/// Resolves the viewer's effective timezone for display.
/// </summary>
/// <remarks>
/// <para>
/// Backed by JS interop, so <see cref="GetTimeZoneAsync"/> must be called <b>after</b> the first
/// render — interop is unavailable while a component is prerendering, and this falls back to the
/// app-wide <c>Sergin:TimeZone</c> default until then.
/// </para>
/// <para>
/// Best-effort: a browser whose reported id doesn't resolve, or that refuses the interop call
/// entirely, degrades to the app-wide default rather than faulting the circuit.
/// </para>
/// </remarks>
public interface IUiTimeZoneStore
{
    /// <summary>
    /// The viewer's browser-detected zone, or the app-wide <c>Sergin:TimeZone</c> default when the
    /// browser hasn't been asked yet (prerender) or its reported id doesn't resolve.
    /// </summary>
    ValueTask<TimeZoneInfo> GetTimeZoneAsync(CancellationToken cancellationToken = default);
}
