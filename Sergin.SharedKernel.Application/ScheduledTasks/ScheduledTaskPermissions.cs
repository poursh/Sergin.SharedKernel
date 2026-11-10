using Sergin.SharedKernel.Domain.Securities;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// The platform's one scheduled-task permission, in the reserved <c>sys</c> schema: the shared UI checks it without
/// knowing which module owns the task. Registered by <see cref="ScheduledTaskAggregateFeatureConfiguration"/>, so it is
/// in the catalogue only in a host where a module opts into scheduled tasks.
/// </summary>
public static class ScheduledTaskPermissions
{
    public static PermissionDefinition Manage { get; } =
        PermissionDefinition.Create("permission.sys.scheduled-tasks.manage", "Manage scheduled tasks");
}
