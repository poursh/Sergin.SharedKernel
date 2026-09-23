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
            // Order matters: EF runs interceptors in registration order, and stamping must see whatever the
            // domain-event handlers added or changed during dispatch.
            .AddInterceptors(
                sp.GetRequiredService<EventDispatcherInterceptor>(),
                sp.GetRequiredService<AuditStampInterceptor>()));

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

            // Same reason for reflection here: OutboxRelaySource<TContext> constrains TContext to
            // IOutboxDbContext, which the runtime check above proves but the compiler cannot see. The
            // schema is the one constructor argument DI cannot supply, so ActivatorUtilities fills in the
            // rest from the provider. Registered as one more IOutboxRelaySource, not the only one — the
            // relay service drains every module's source in turn.
            Type relaySourceType = typeof(OutboxRelaySource<>).MakeGenericType(typeof(TContext));
            services.AddSingleton<IOutboxRelaySource>(sp => (IOutboxRelaySource)ActivatorUtilities.CreateInstance(sp, relaySourceType, schema));
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
