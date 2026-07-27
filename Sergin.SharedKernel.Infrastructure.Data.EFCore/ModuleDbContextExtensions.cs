using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore;

public static class ModuleDbContextExtensions
{
    public static IServiceCollection AddModuleDbContext<TContext, TIContext, TIUnitOfWork>(
        this IServiceCollection services,
        IConfigurationSection configuration,
        string schema)
        where TContext : SerginDbContext, TIContext, TIUnitOfWork
        where TIContext : class
        where TIUnitOfWork : class
    {
        string connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Sergin:ConnectionStrings:Database' is not configured.");

        services.AddDbContext<TContext>((sp, options) =>
            options.UseNpgsql(
                connectionString,
                pgOptions => pgOptions.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(sp.GetRequiredService<EventDispatcherInterceptor>()));

        services.AddScoped<TIContext>(p => p.GetRequiredService<TContext>());
        services.AddScoped<TIUnitOfWork>(p => p.GetRequiredService<TContext>());

        return services;
    }

    public static async Task MigrateDbContextAsync<TContext>(this IServiceProvider services)
        where TContext : DbContext
    {
        using IServiceScope scope = services.CreateScope();
        using TContext context = scope.ServiceProvider.GetRequiredService<TContext>();

        await context.Database.MigrateAsync();
    }
}
