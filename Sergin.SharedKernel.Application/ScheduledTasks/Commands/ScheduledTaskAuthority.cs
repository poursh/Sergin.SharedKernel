using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Securities.Authorization;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>
/// The early refusal for scheduling a command: the caller must hold the scheduled command's own required permissions
/// as well as <see cref="ScheduledTaskPermissions.Manage"/> (which the pipeline checks). Each is checked where the
/// pipeline would check it for the command itself: on the current scope for <see cref="PermissionScope.Current"/>, on
/// any scope for <see cref="PermissionScope.Any"/>. Every run checks them again, item by item, as the real guarantee.
/// </summary>
internal sealed class ScheduledTaskAuthority(IScopeAuthorization auth, CommandConfigurationRegistry registry)
{
    public async Task<ErrorOr<Success>> EnsureMayScheduleAsync(Type requestType, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        CommandSettings settings = registry.For(requestType);

        foreach (PermissionDefinition permission in settings.RequiredPermissions)
        {
            bool held = settings.PermissionScope == PermissionScope.Any
                ? await auth.CurrentUserHoldsAnywhereAsync(permission, ct)
                : await auth.CurrentUserHasAsync(permission, ct);

            if (!held)
            {
                return Error.Forbidden();
            }
        }

        return Result.Success;
    }
}
