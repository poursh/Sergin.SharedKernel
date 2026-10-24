namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>The fallback <see cref="IUserDirectory"/>: knows no one, so every id is shown as itself.</summary>
internal sealed class EmptyUserDirectory : IUserDirectory
{
    public Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyDictionary<Guid, string>>(new Dictionary<Guid, string>());
}
