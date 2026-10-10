namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class CommandConfigurationBuilder
{
    private protected List<Permission> Permissions { get; } = [];

    private protected bool ExpectedVersionRequired { get; set; }

    private protected bool InScope { get; set; }

    internal CommandSettings Settings =>
        Permissions.Count == 0 && !ExpectedVersionRequired && !InScope
            ? CommandSettings.None
            : new CommandSettings([.. Permissions.Distinct()], ExpectedVersionRequired, InScope);
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

    /// <summary>
    /// The required permissions are checked on the scope <c>IAuthorizationScopeContext</c> holds instead of the
    /// global claims alone: a permission held globally still passes, and a grant on that scope (or above it, over
    /// the subtree) passes too. Opt in only for a request whose data the current scope actually limits. The
    /// registry refuses it on a configuration that requires no permission.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> InCurrentScope()
    {
        InScope = true;
        return this;
    }
}
