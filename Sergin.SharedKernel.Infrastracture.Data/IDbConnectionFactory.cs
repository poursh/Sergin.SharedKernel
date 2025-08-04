using System.Data.Common;

namespace Sergin.SharedKernel.Infrastracture.Data;

public interface IDbConnectionFactory
{
    ValueTask<DbConnection> CreateConnectionAsync();
}
