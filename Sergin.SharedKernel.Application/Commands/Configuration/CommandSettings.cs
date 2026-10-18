namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What a request's configuration declared. <see cref="None"/> is the answer for an unconfigured request: no
/// permission and no expected version required. Every required permission is checked on the scope
/// <c>IAuthorizationScopeContext</c> holds, or on the root scope when <see cref="PermissionsAtRoot"/> is set: the
/// registry's answer, from <c>AtRoot()</c>/<c>InCurrentScope()</c>, else from <see cref="AggregateRoot"/>'s
/// features. <see cref="AggregateRoot"/> is the root the configuration bound the request to, or null.
/// </summary>
public sealed record CommandSettings(
    IReadOnlyCollection<Permission> RequiredPermissions,
    bool RequiresExpectedVersion,
    bool PermissionsAtRoot = false,
    Type? AggregateRoot = null)
{
    public static CommandSettings None { get; } = new([], false);
}
