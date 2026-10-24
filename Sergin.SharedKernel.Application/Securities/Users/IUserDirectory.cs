namespace Sergin.SharedKernel.Application.Securities.Users;

/// <summary>
/// Turns Sergin user ids into display names — for audit stamps and anything else that stores a user id and shows
/// a person. Implemented by the module that owns the user store; <c>AddSerginCore</c> registers a fallback that
/// knows no one, so a host without that module shows ids instead of failing.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// The display name of every id the store knows; an unknown id is simply absent from the answer.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, string>> GetDisplayNamesAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken = default);
}
