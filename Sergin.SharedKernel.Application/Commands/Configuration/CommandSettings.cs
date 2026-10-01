namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What a request's configuration declared. <see cref="None"/> is the answer for an unconfigured request: no
/// permission and no expected version required.
/// </summary>
public sealed record CommandSettings(IReadOnlyCollection<Permission> RequiredPermissions, bool RequiresExpectedVersion)
{
    public static CommandSettings None { get; } = new([], false);
}
