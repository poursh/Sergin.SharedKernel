namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>The fallback <see cref="IScopedPermissionChecker"/> for a host no scope-owning module is composed into.</summary>
public sealed class DenyAllScopedPermissionChecker : IScopedPermissionChecker
{
    public Task<bool> HasPermissionAsync(Guid userId, Permission permission, Guid scopeId, CancellationToken cancellationToken = default) =>
        Task.FromResult(false);
}
