using Microsoft.EntityFrameworkCore;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore;

public interface IDbContext
{
    DbSet<TEntity> Set<TEntity>() where TEntity : class;
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
