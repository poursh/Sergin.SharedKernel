namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What a request's configuration declared. <see cref="None"/> is the answer for an unconfigured request: no
/// permission and no expected version required. <see cref="PermissionScope"/> is where the required permissions are
/// checked, the registry's answer from <see cref="AggregateRoot"/>'s features: <see cref="PermissionScope.Current"/>
/// for an unbound request or a <c>Scoped()</c> root, <see cref="PermissionScope.Any"/> for any other root.
/// <see cref="AggregateRoot"/> is the root the configuration bound the request to, or null.
/// </summary>
public sealed record CommandSettings(
    IReadOnlyCollection<Permission> RequiredPermissions,
    bool RequiresExpectedVersion,
    PermissionScope PermissionScope = PermissionScope.Current,
    Type? AggregateRoot = null)
{
    public static CommandSettings None { get; } = new([], false);
}
