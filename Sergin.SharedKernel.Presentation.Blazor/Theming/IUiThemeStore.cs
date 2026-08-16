namespace Sergin.SharedKernel.Presentation.Blazor.Theming;

/// <summary>
/// Persists the shell's appearance choices across reloads.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are <b>best-effort</b>: a browser that refuses storage (private mode, disabled
/// site data, exhausted quota) degrades to the default theme rather than faulting the circuit, so
/// no method throws for a storage failure. Callers therefore cannot distinguish "nothing stored"
/// from "storage unavailable", and do not need to.
/// </para>
/// <para>
/// Backed by JS interop, so every method must be called <b>after</b> the first render — interop is
/// unavailable while a component is prerendering.
/// </para>
/// </remarks>
public interface IUiThemeStore
{
    /// <summary>
    /// Reads the stored appearance, or <see langword="null"/> when nothing usable is stored.
    /// </summary>
    ValueTask<StoredUiTheme?> LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Writes the appearance, replacing whatever was stored before.
    /// </summary>
    ValueTask SaveAsync(StoredUiTheme theme, CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets the stored appearance, so the next load falls back to the shell's defaults.
    /// </summary>
    ValueTask ClearAsync(CancellationToken cancellationToken = default);
}
