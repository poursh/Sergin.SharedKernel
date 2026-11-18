using System.Data.Common;
using Npgsql;
using Sergin.SharedKernel.Infrastracture.Data;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore;

internal sealed class PostgresDbConnectionFactory(string connectionString) : IDbConnectionFactory
{
    public async ValueTask<DbConnection> CreateConnectionAsync()
    {
        NpgsqlConnection connection = new(connectionString);

        await connection.OpenAsync();

        return connection;
    }
}
