using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TickerQ.DependencyInjection;
using TickerQ.EntityFrameworkCore.DbContextFactory;
using TickerQ.EntityFrameworkCore.DependencyInjection;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

public static class TickerQEngineExtensions
{
    public const string Schema = "tickerq";

    /// <summary>
    /// Registers the TickerQ engine against its own <c>tickerq</c> schema. No dashboard.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="serginSection">The <c>Sergin</c> configuration section.</param>
    /// <param name="connectionString">The database connection string.</param>
    public static IServiceCollection AddSerginTickerQ(this IServiceCollection services, IConfiguration serginSection, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(serginSection);

        services.AddOptions<TickerQScheduledTaskOptions>()
            .Bind(serginSection.GetSection(TickerQScheduledTaskOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<TickerQScheduledTaskOptions>, TickerQLicenseGuard>();

        string licensePath = serginSection.GetSection(TickerQScheduledTaskOptions.SectionName)[nameof(TickerQScheduledTaskOptions.LicensePath)] ?? string.Empty;

        services.AddTickerQ(options =>
        {
            if (licensePath.Length > 0)
            {
                options.UseLicense(licensePath);
            }

            options.UseApplicationNamespace("sergin");
            options.AddOperationalStore(store => store.UseTickerQDbContext<TickerQDbContext>(
                db => db.UseNpgsql(connectionString),
                schema: Schema));
        });

        return services;
    }
}
