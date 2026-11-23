using MudBlazor;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Navigation;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks.Runs;

/// <summary>
/// The shell's Task runs entry in the drawer: Administration, under the caption "Tasks". The host adds it only when a
/// module opts into tasks, so there is no link to a page with nothing to list.
/// </summary>
public static class SerginTaskNavigation
{
    public static SerginNavItem Runs { get; } = new(
        TaskUiTexts.Navigation.TaskRuns,
        SerginTaskRunsPage.Route,
        Icons.Material.Filled.WorkHistory,
        400,
        TaskPermissions.Manage);

    public static SerginShellNavEntry Entry { get; } = new(SerginNavGroups.Administration, TaskUiTexts.Navigation.Caption, [Runs]);
}
