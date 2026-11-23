using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Tasks;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.EntityFrameworkCore.DependencyInjection;

namespace Sergin.SharedKernel.Infrastructure.Tasks.TickerQ;

public static class TickerQEngineExtensions
{
    public const string Schema = "tickerq";

    /// <summary>
    /// Registers Sergin's <see cref="ITaskEngine"/>. With <c>Sergin:Tasks:TickerQ:LicensePath</c> set:
    /// the TickerQ engine against its own <c>tickerq</c> schema (no dashboard; TickerQ's tables are not created here, a
    /// host creates them before it starts), and a licence guard that refuses start when the path names no file, so a
    /// typo never falls back. Blank or absent: <see cref="InProcessTaskEngine"/> and its worker instead, and no
    /// TickerQ service at all; schedules are kept and never fire, run items execute in process.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serginSection">The <c>Sergin</c> configuration section.</param>
    /// <param name="connectionString">The database connection string.</param>
    public static IServiceCollection AddSerginTickerQ(this IServiceCollection services, IConfiguration serginSection, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serginSection);

        IConfigurationSection section = serginSection.GetSection(TickerQTaskOptions.SectionName);
        OptionsBuilder<TickerQTaskOptions> options = services.AddOptions<TickerQTaskOptions>().Bind(section);
        string licensePath = section[nameof(TickerQTaskOptions.LicensePath)] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(licensePath))
        {
            services.AddSingleton<InProcessTaskEngine>();
            services.AddSingleton<ITaskEngine>(provider => provider.GetRequiredService<InProcessTaskEngine>());
            services.AddHostedService<InProcessRunItemWorker>();

            return services;
        }

        options.ValidateOnStart();
        services.AddSingleton<IValidateOptions<TickerQTaskOptions>, TickerQLicenseGuard>();

        services.AddTickerQ(tickerQ =>
        {
            tickerQ.UseLicense(licensePath);
            tickerQ.UseApplicationNamespace("sergin");
            tickerQ.AddOperationalStore(store => store.UseTickerQDbContext<TickerQDbContext>(
                db => db.UseNpgsql(connectionString),
                schema: Schema));
        });

        // The engine Sergin calls: a singleton over TickerQ's singleton managers. The concrete type is registered too, so
        // the run-item function can enqueue a retry at a later time, which the engine interface does not offer.
        services.AddSingleton<TickerQTaskEngine>();
        services.AddSingleton<ITaskEngine>(provider => provider.GetRequiredService<TickerQTaskEngine>());

        return services;
    }

    /// <summary>
    /// Activates TickerQ when <see cref="AddSerginTickerQ"/> registered it, and does nothing for the in-process stand-in,
    /// so a host calls this one method whichever engine its configuration chose.
    /// </summary>
    /// <param name="host">The built host.</param>
    public static IHost UseSerginTickerQ(this IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (UsesTickerQ(host.Services))
        {
            host.UseTickerQ();
        }

        return host;
    }

    /// <summary>
    /// Development only: creates TickerQ's tables in the <c>tickerq</c> schema when the real engine is registered and the
    /// schema holds no table yet, so a developer with a licence file needs no manual step after a database reset. Call it
    /// after the modules' migrations (they create the database) and before the host starts: TickerQ's cluster-control
    /// service reads these tables during start. Does nothing for the stand-in, outside Development, or when any table
    /// already exists; a real environment provides the tables itself.
    /// </summary>
    /// <param name="host">The built host, not yet started.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public static async Task EnsureSerginTickerQStoreAsync(this IHost host, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(host);

        if (!UsesTickerQ(host.Services) || !host.Services.GetRequiredService<IHostEnvironment>().IsDevelopment())
        {
            return;
        }

        IDbContextFactory<TickerQDbContext> contexts = host.Services.GetRequiredService<IDbContextFactory<TickerQDbContext>>();
        await using TickerQDbContext context = await contexts.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        bool hasTables = await context.Database
            .SqlQuery<bool>($"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = {Schema}) AS \"Value\"")
            .SingleAsync(cancellationToken)
            .ConfigureAwait(false);

        if (hasTables)
        {
            return;
        }

        await context.Database.ExecuteSqlRawAsync("CREATE SCHEMA IF NOT EXISTS tickerq;", cancellationToken).ConfigureAwait(false);
        await context.GetService<IRelationalDatabaseCreator>().CreateTablesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static bool UsesTickerQ(IServiceProvider services) =>
        services.GetService<IServiceProviderIsService>()?.IsService(typeof(TickerQTaskEngine)) == true;
}
