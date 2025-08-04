
using Microsoft.EntityFrameworkCore;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore;

public abstract class SerginDbContext(DbContextOptions options) : DbContext(options), IDbContext
{
    
}
