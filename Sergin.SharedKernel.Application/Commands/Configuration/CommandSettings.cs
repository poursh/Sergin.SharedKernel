namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What a request's configuration declared. <see cref="None"/> is the answer for an unconfigured request: no
/// permission and no expected version required. <see cref="PermissionsInCurrentScope"/> moves the permission check
/// from the global claims to the scope <c>IAuthorizationScopeContext</c> holds.
/// </summary>
public sealed record CommandSettings(
    IReadOnlyCollection<Permission> RequiredPermissions,
    bool RequiresExpectedVersion,
    bool PermissionsInCurrentScope = false)
{
    public static CommandSettings None { get; } = new([], false);
}
