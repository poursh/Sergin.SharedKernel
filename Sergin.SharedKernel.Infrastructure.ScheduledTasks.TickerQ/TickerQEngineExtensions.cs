using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.ScheduledTasks;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.EntityFrameworkCore.DependencyInjection;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

public static class TickerQEngineExtensions
{
    public const string Schema = "tickerq";

    /// <summary>
    /// Registers Sergin's <see cref="IScheduledTaskEngine"/>. With <c>Sergin:ScheduledTasks:TickerQ:LicensePath</c> set:
    /// the TickerQ engine against its own <c>tickerq</c> schema (no dashboard; TickerQ's tables are not created here, a
    /// host creates them before it starts), and a licence guard that refuses start when the path names no file, so a
    /// typo never falls back. Blank or absent: <see cref="InProcessScheduledTaskEngine"/> and its worker instead, and no
    /// TickerQ service at all; schedules are kept and never fire, run items execute in process.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serginSection">The <c>Sergin</c> configuration section.</param>
    /// <param name="connectionString">The database connection string.</param>
    public static IServiceCollection AddSerginTickerQ(this IServiceCollection services, IConfiguration serginSection, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serginSection);

        IConfigurationSection section = serginSection.GetSection(TickerQScheduledTaskOptions.SectionName);
        OptionsBuilder<TickerQScheduledTaskOptions> options = services.AddOptions<TickerQScheduledTaskOptions>().Bind(section);
        string licensePath = section[nameof(TickerQScheduledTaskOptions.LicensePath)] ?? string.Empty;

        if (string.IsNullOrWhiteSpace(licensePath))
        {
            services.AddSingleton<InProcessScheduledTaskEngine>();
            services.AddSingleton<IScheduledTaskEngine>(provider => provider.GetRequiredService<InProcessScheduledTaskEngine>());
            services.AddHostedService<InProcessRunItemWorker>();

            return services;
        }

        options.ValidateOnStart();
        services.AddSingleton<IValidateOptions<TickerQScheduledTaskOptions>, TickerQLicenseGuard>();

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
        services.AddSingleton<TickerQScheduledTaskEngine>();
        services.AddSingleton<IScheduledTaskEngine>(provider => provider.GetRequiredService<TickerQScheduledTaskEngine>());

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

        if (host.Services.GetService<IServiceProviderIsService>()?.IsService(typeof(TickerQScheduledTaskEngine)) == true)
        {
            host.UseTickerQ();
        }

        return host;
    }
}
