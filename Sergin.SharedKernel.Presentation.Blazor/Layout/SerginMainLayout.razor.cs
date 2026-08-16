using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MudBlazor;
using MudBlazor.ThemeManager;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Presentation.Blazor.Theming;

namespace Sergin.SharedKernel.Presentation.Blazor.Layout;

public sealed partial class SerginMainLayout
{
    private bool drawerOpen = true;
    private bool themeManagerOpen;
    private bool isDarkMode;

    /// <summary>
    /// Backs both the <c>MudThemeProvider</c> and the theme manager drawer, so the shell renders the
    /// same theme the tool edits. The elevations are seeded with the shell's own previous hard-coded
    /// values rather than left at <see cref="ThemeManagerTheme"/>'s defaults, so adding the tool
    /// changed nothing about how the shell looks on first render.
    /// </summary>
    private ThemeManagerTheme themeManagerTheme = new()
    {
        AppBarElevation = 1,
        DrawerElevation = 1,
    };

    [Inject]
    private IOptions<SerginApplicationOptions> ApplicationOptions { get; set; } = default!;

    [Inject]
    private IHostEnvironment HostEnvironment { get; set; } = default!;

    [Inject]
    private ILocalizer Localizer { get; set; } = default!;

    [Inject]
    private IUiThemeStore ThemeStore { get; set; } = default!;

    /// <summary>
    /// Resolves to <see cref="SerginApplicationOptions.ApplicationName"/>'s default when a host never bound
    /// the section, so the shell renders a sensible title rather than an empty app bar.
    /// </summary>
    private string ApplicationName => ApplicationOptions.Value.ApplicationName;

    /// <summary>
    /// The theme manager is a design-time tool, not an end-user preference panel: it edits the theme
    /// for the current circuit only and persists nothing. Development-only keeps it out of any host
    /// that eventually serves real users.
    /// </summary>
    private bool ShowThemeManager => HostEnvironment.IsDevelopment();

    /// <summary>
    /// Shows the mode being switched *to*, which is the convention users expect from a mode toggle —
    /// a sun means "go light", not "you are light".
    /// </summary>
    private string DarkModeIcon => isDarkMode
        ? Icons.Material.Outlined.LightMode
        : Icons.Material.Outlined.DarkMode;

    private string DarkModeLabel => Localizer[isDarkMode ? "Switch to light mode" : "Switch to dark mode"];

    /// <summary>
    /// Applies the stored appearance, if any.
    /// </summary>
    /// <remarks>
    /// This runs on the first rendered frame rather than in <c>OnInitializedAsync</c> because the store
    /// reads <c>localStorage</c> over JS interop, which does not exist yet while the component is
    /// prerendering. The visible consequence is that a reload paints the default theme for one frame
    /// before the stored one replaces it.
    /// </remarks>
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender)
        {
            return;
        }

        StoredUiTheme? stored = await ThemeStore.LoadAsync();

        if (stored is null)
        {
            return;
        }

        themeManagerTheme = stored.Theme;
        isDarkMode = stored.IsDarkMode;

        StateHasChanged();
    }

    private void ToggleDrawer() => drawerOpen = !drawerOpen;

    /// <summary>
    /// Flips the palette the <c>MudThemeProvider</c> renders. Unlike the theme manager this is a normal
    /// user-facing control, so it is deliberately not gated on <see cref="ShowThemeManager"/> — but it is
    /// what makes the manager's dark palette reachable, since the manager itself exposes no mode switch.
    /// </summary>
    private async Task ToggleDarkMode()
    {
        isDarkMode = !isDarkMode;

        await PersistAsync();
    }

    private void OpenThemeManager() => themeManagerOpen = true;

    private async Task OnThemeChanged(ThemeManagerTheme theme)
    {
        themeManagerTheme = theme;

        await PersistAsync();
    }

    private ValueTask PersistAsync() => ThemeStore.SaveAsync(new StoredUiTheme(themeManagerTheme, isDarkMode));
}
