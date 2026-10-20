namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class CommandConfigurationBuilder
{
    internal List<PermissionDefinition> Permissions { get; } = [];

    internal bool ExpectedVersionRequired { get; private protected set; }
}

public sealed class CommandConfigurationBuilder<TCommand> : CommandConfigurationBuilder
    where TCommand : IBaseCommand
{
    internal CommandConfigurationBuilder()
    {
    }

    /// <summary>
    /// The caller must hold every listed permission, each a definition an aggregate registered with
    /// <c>Permissions(...)</c> (or <see cref="PermissionDefinition.AllPlatform"/>): the registry refuses any other.
    /// Calling it again appends; a repeated permission counts once.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> RequirePermissions(PermissionDefinition permission, params PermissionDefinition[] more)
    {
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentNullException.ThrowIfNull(more);

        Permissions.Add(permission);
        Permissions.AddRange(more);
        return this;
    }

    /// <summary>
    /// A send without an expected version answers <c>VersionErrors.Required</c> (428) before the handler runs.
    /// Commands only: the registry refuses it on a query, and on a command bound to an aggregate that is not
    /// <c>Versioned()</c>.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> RequireExpectedVersion()
    {
        ExpectedVersionRequired = true;
        return this;
    }
}
