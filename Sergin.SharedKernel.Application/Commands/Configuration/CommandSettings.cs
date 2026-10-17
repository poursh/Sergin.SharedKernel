namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What a request's configuration declared. <see cref="None"/> is the answer for an unconfigured request: no
/// permission and no expected version required. Every required permission is checked on the scope
/// <c>IAuthorizationScopeContext</c> holds, or on the root scope when <see cref="PermissionsAtRoot"/> is set.
/// </summary>
public sealed record CommandSettings(
    IReadOnlyCollection<Permission> RequiredPermissions,
    bool RequiresExpectedVersion,
    bool PermissionsAtRoot = false)
{
    public static CommandSettings None { get; } = new([], false);
}
