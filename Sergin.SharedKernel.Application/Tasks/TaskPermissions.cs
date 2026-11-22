using Sergin.SharedKernel.Domain.Securities;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// The platform's one task permission, in the reserved <c>sys</c> schema: the shared UI checks it without
/// knowing which module owns the task. Registered by <see cref="TaskScheduleAggregateFeatureConfiguration"/>, so it is
/// in the catalogue only in a host where a module opts into tasks.
/// </summary>
public static class TaskPermissions
{
    public static PermissionDefinition Manage { get; } =
        PermissionDefinition.Create("permission.sys.tasks.manage", "Manage scheduled tasks");
}
