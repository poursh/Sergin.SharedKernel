namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class CommandConfigurationBuilder
{
    private protected List<Permission> Permissions { get; } = [];

    private protected bool ExpectedVersionRequired { get; set; }

    private protected bool CheckedAtRoot { get; set; }

    internal CommandSettings Settings =>
        Permissions.Count == 0 && !ExpectedVersionRequired && !CheckedAtRoot
            ? CommandSettings.None
            : new CommandSettings([.. Permissions.Distinct()], ExpectedVersionRequired, CheckedAtRoot);
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
    /// The required permissions are checked on the root scope instead of the scope <c>IAuthorizationScopeContext</c>
    /// holds. For a request over global data, which belongs to no scope below the root: users, the device catalog.
    /// Without it, every permission is checked on the current scope. The registry refuses it on a configuration
    /// that requires no permission.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> AtRoot()
    {
        CheckedAtRoot = true;
        return this;
    }
}
