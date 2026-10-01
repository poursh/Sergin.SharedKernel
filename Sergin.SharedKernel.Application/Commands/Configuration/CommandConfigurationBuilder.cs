namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class CommandConfigurationBuilder
{
    private protected List<Permission> Permissions { get; } = [];

    private protected bool ExpectedVersionRequired { get; set; }

    internal CommandSettings Settings =>
        Permissions.Count == 0 && !ExpectedVersionRequired
            ? CommandSettings.None
            : new CommandSettings([.. Permissions.Distinct()], ExpectedVersionRequired);
}

public sealed class CommandConfigurationBuilder<TCommand> : CommandConfigurationBuilder
    where TCommand : IBaseCommand
{
    internal CommandConfigurationBuilder()
    {
    }

    /// <summary>
    /// The caller must hold every listed permission. Calling it again appends; a repeated permission counts once.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> RequirePermissions(Permission permission, params Permission[] more)
    {
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentNullException.ThrowIfNull(more);

        Permissions.Add(permission);
        Permissions.AddRange(more);
        return this;
    }

    /// <summary>
    /// A send without an expected version answers <c>VersionErrors.Required</c> (428) before the handler runs.
    /// Commands only: the registry refuses it on a query.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> RequireExpectedVersion()
    {
        ExpectedVersionRequired = true;
        return this;
    }
}
