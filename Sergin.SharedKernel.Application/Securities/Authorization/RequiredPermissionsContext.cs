namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// The permissions the request being handled required, published by <c>PermissionCheckPipelineBehavior</c> once
/// they passed. The save checks each again on the scope of every record it writes, so a request allowed at one
/// scope cannot change a record that lives elsewhere. Empty outside a configured request: an unconfigured request,
/// system code.
/// <para>
/// Registered scoped, next to <see cref="IAuthorizationScopeContext"/>: one value per send.
/// </para>
/// </summary>
public sealed class RequiredPermissionsContext
{
    public IReadOnlyCollection<PermissionDefinition> Permissions { get; private set; } = [];

    /// <summary>
    /// Publishes <paramref name="permissions"/> until the returned handle is disposed, which restores the previous
    /// value, so a request sent from inside another's handler leaves the outer request's permissions in place.
    /// </summary>
    public IDisposable Publish(IReadOnlyCollection<PermissionDefinition> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        Restore restore = new(this, Permissions);
        Permissions = permissions;
        return restore;
    }

    private sealed class Restore(RequiredPermissionsContext context, IReadOnlyCollection<PermissionDefinition> previous) : IDisposable
    {
        public void Dispose() => context.Permissions = previous;
    }
}
