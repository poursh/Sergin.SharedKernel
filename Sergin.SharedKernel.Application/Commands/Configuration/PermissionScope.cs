namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// Where a request's required permissions are checked. The registry decides it from the request's aggregate; a
/// configuration cannot choose it.
/// </summary>
public enum PermissionScope
{
    /// <summary>
    /// On the scope <c>IAuthorizationScopeContext</c> holds: an untyped request, or one whose type names a <c>Scoped()</c>
    /// aggregate, whose records live in scopes.
    /// </summary>
    Current,

    /// <summary>
    /// On any scope: the caller passes when they hold every permission somewhere. A request whose type names an aggregate
    /// that is not <c>Scoped()</c>: its records belong to no scope.
    /// </summary>
    Any,
}
