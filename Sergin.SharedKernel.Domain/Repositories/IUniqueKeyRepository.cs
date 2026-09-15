namespace Sergin.SharedKernel.Domain.Repositories;

/// <summary>
/// Declares that <typeparamref name="TKey"/> is an alternate key of the repository's aggregate — at most one
/// row may carry a given value. A repository implements it once per unique value object
/// (<c>IDeviceRepository : IRepository&lt;Device, DeviceIntenralId&gt;, IUniqueKeyRepository&lt;DeviceId&gt;</c>).
/// The check is advisory: the same PR must add a unique index on the column, which is the actual guarantee under
/// concurrency.
/// </summary>
/// <remarks>
/// Only the key type is a type parameter, on purpose: a value-object type is already specific to one aggregate,
/// and a repository may implement this interface for several keys — a second (aggregate) type parameter would
/// defeat inference in <c>MustBeUniqueIn</c> for such a repository.
/// </remarks>
public interface IUniqueKeyRepository<in TKey>
    where TKey : notnull
{
    Task<bool> IsTakenAsync(TKey key, CancellationToken cancellationToken = default);
}
