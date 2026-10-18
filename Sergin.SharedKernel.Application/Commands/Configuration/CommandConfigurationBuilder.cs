namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class CommandConfigurationBuilder
{
    internal List<Permission> Permissions { get; } = [];

    internal bool ExpectedVersionRequired { get; private protected set; }

    /// <summary>
    /// <c>true</c> for <c>AtRoot()</c>, <c>false</c> for <c>InCurrentScope()</c>, <c>null</c> when neither was
    /// called and the registry derives the scope from the request's aggregate.
    /// </summary>
    internal bool? CheckedAtRoot { get; private protected set; }
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
    /// Commands only: the registry refuses it on a query, and on a command bound to an aggregate that is not
    /// <c>Versioned()</c>.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> RequireExpectedVersion()
    {
        ExpectedVersionRequired = true;
        return this;
    }

    /// <summary>
    /// The required permissions are checked on the root scope instead of the scope <c>IAuthorizationScopeContext</c>
    /// holds, and reads of scoped aggregates cover the whole tree, still narrowed to the caller's reach. A request
    /// bound to an aggregate that is not <c>Scoped()</c> gets this without calling it; an unbound request, or one
    /// bound to a scoped aggregate, needs it said. The registry refuses it on a configuration that requires no
    /// permission.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> AtRoot()
    {
        CheckedAtRoot = true;
        return this;
    }

    /// <summary>
    /// The required permissions are checked on the scope <c>IAuthorizationScopeContext</c> holds, even when the
    /// request is bound to an aggregate that is not <c>Scoped()</c> and would be checked on the root scope. For a
    /// read of global data that every scope needs, such as the device catalog a regional user picks from. An
    /// unbound request, or one bound to a scoped aggregate, gets this without calling it. The registry refuses it
    /// on a configuration that requires no permission.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> InCurrentScope()
    {
        CheckedAtRoot = false;
        return this;
    }
}
