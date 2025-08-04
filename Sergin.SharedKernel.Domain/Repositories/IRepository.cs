
namespace Sergin.SharedKernel.Domain.Repositories;

public interface IRepository;

public interface IRepository<TAggregateRoot, TId>
    where TAggregateRoot : class, IAggregateRoot<TId>
    where TId : notnull
{
    ValueTask<TAggregateRoot?> GetAsync(TId id, CancellationToken cancellationToken = default);    
    void Insert(TAggregateRoot entity);
    void Remove(TAggregateRoot entity);
}
