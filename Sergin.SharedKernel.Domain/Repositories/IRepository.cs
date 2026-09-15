
namespace Sergin.SharedKernel.Domain.Repositories;

public interface IRepository;

public interface IRepository<TAggregateRoot, TId>
    where TAggregateRoot : class, IAggregateRoot<TId>
    where TId : notnull
{
    ValueTask<TAggregateRoot?> GetAsync(TId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Answers whether an aggregate with this id exists without loading or tracking it — a
    /// <c>SELECT EXISTS</c>, not a <see cref="GetAsync"/> followed by a null check.
    /// </summary>
    Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default);

    void Insert(TAggregateRoot entity);
    void Remove(TAggregateRoot entity);
}
