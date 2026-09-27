using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Domain.Repositories;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Repositories;

/// <summary>
/// The EF-backed <see cref="IRepository{TAggregateRoot, TId}"/> a module's write-side repository derives from,
/// so it keeps only its named lookups (<c>GetByUserName</c>, <c>GetByDeviceId</c>, …) and any
/// <see cref="IUniqueKeyRepository{TKey}.IsTakenAsync"/> it declares — and so the next
/// <see cref="IRepository{TAggregateRoot, TId}"/> member is a change here alone, not in every module.
/// </summary>
public abstract class EfRepository<TAggregateRoot, TId>(IDbContext dbContext) : IRepository<TAggregateRoot, TId>
    where TAggregateRoot : class, IAggregateRoot<TId>
    where TId : notnull
{
    protected DbSet<TAggregateRoot> Set => dbContext.Set<TAggregateRoot>();

    /// <summary>
    /// Another entity type's set, for a lookup that has to reach across the aggregate boundary to answer a
    /// yes/no question (<c>DeviceRepository.AnyUsingManufacturerAsync</c>). A derived repository cannot capture its
    /// own constructor parameter for this: passing it to this base as well is CS9107.
    /// </summary>
    protected DbSet<TEntity> SetOf<TEntity>()
        where TEntity : class
        => dbContext.Set<TEntity>();

    public ValueTask<TAggregateRoot?> GetAsync(TId id, CancellationToken cancellationToken = default)
        => Set.FindAsync([id], cancellationToken);

    /// <summary>
    /// <c>SELECT EXISTS</c> — nothing is loaded or tracked, unlike <c>await GetAsync(id) is not null</c>.
    /// </summary>
    public Task<bool> ExistsAsync(TId id, CancellationToken cancellationToken = default)
        => Set.AnyAsync(entity => entity.Id.Equals(id), cancellationToken);

    /// <summary>
    /// For a derived class's <see cref="IUniqueKeyRepository{TKey}.IsTakenAsync"/>:
    /// <c>AnyAsync(device => device.DeviceId == key, cancellationToken)</c>.
    /// </summary>
    protected Task<bool> AnyAsync(Expression<Func<TAggregateRoot, bool>> predicate, CancellationToken cancellationToken)
        => Set.AnyAsync(predicate, cancellationToken);

    public void Insert(TAggregateRoot entity) => Set.Add(entity);

    public void Remove(TAggregateRoot entity) => Set.Remove(entity);
}
