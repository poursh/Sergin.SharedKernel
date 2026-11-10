namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What a request's configuration declared and its type implies. <see cref="None"/> is the answer for an unconfigured
/// untyped request: no permission and no expected version required. <see cref="PermissionScope"/> is where the required
/// permissions are checked, the registry's answer from <see cref="AggregateRoot"/>'s features:
/// <see cref="PermissionScope.Current"/> for an untyped request or a <c>Scoped()</c> root, <see cref="PermissionScope.Any"/>
/// for any other root. <see cref="AggregateRoot"/> is the root the request's type names, or null for an untyped one.
/// <see cref="ScheduleName"/> is the stable name the configuration gave with <c>Schedulable(name)</c>, or null.
/// </summary>
public sealed record CommandSettings(
    IReadOnlyCollection<PermissionDefinition> RequiredPermissions,
    bool RequiresExpectedVersion,
    PermissionScope PermissionScope = PermissionScope.Current,
    Type? AggregateRoot = null,
    string? ScheduleName = null)
{
    public static CommandSettings None { get; } = new([], false);
}
