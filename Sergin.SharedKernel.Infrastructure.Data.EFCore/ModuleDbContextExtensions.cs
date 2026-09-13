using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

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

        if (typeof(IOutboxDbContext).IsAssignableFrom(typeof(TContext)))
        {
            // Neither generic argument here is statically known to satisfy IInbox<TUnitOfWork>'s own
            // TUnitOfWork : IUnitOfWork constraint (TIUnitOfWork is only constrained to `class` above), so
            // both the service type and EfInbox<,> itself are built through reflection rather than a closed
            // generic reference the compiler could check.
            Type inboxServiceType = typeof(IInbox<>).MakeGenericType(typeof(TIUnitOfWork));
            Type inboxImplementationType = typeof(EfInbox<,>).MakeGenericType(typeof(TContext), typeof(TIUnitOfWork));
            services.AddScoped(inboxServiceType, inboxImplementationType);
        }

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
