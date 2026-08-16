using MudBlazor.ThemeManager;

namespace Sergin.SharedKernel.Presentation.Blazor.Theming;

/// <summary>
/// The shell appearance choices that survive a reload.
/// </summary>
/// <param name="Theme">
/// The MudBlazor theme, in the same shape the Development-only theme manager edits. Round-trips
/// through <see cref="System.Text.Json.JsonSerializer"/> — including every <c>MudColor</c>, which
/// deserializes back to an equal colour written in <c>rgba(...)</c> form.
/// </param>
/// <param name="IsDarkMode">Whether the dark palette is the active one.</param>
public sealed record StoredUiTheme(ThemeManagerTheme Theme, bool IsDarkMode);
