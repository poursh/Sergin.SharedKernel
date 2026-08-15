namespace Sergin.SharedKernel.Presentation.Blazor.Layout;

public sealed partial class SerginMainLayout
{
    private bool drawerOpen = true;

    private void ToggleDrawer() => drawerOpen = !drawerOpen;
}
