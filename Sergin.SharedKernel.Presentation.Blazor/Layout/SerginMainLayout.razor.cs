using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Presentation.Blazor.Layout;

public sealed partial class SerginMainLayout
{
    private bool drawerOpen = true;

    [Inject]
    private IOptions<SerginApplicationOptions> ApplicationOptions { get; set; } = default!;

    /// <summary>
    /// Resolves to <see cref="SerginApplicationOptions.ApplicationName"/>'s default when a host never bound
    /// the section, so the shell renders a sensible title rather than an empty app bar.
    /// </summary>
    private string ApplicationName => ApplicationOptions.Value.ApplicationName;

    private void ToggleDrawer() => drawerOpen = !drawerOpen;
}
