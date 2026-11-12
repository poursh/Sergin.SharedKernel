using System.Globalization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Hosts.Authentication;
using Sergin.SharedKernel.Hosts.Commands;
using Sergin.SharedKernel.Hosts.Localizations;
using Sergin.SharedKernel.Hosts.WebApi.Localizations;
using Sergin.SharedKernel.Infrastracture.WebApi.Users;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.WebApi.Authorization;
using Sergin.SharedKernel.Presentation.Localizations;
using Sergin.SharedKernel.Presentation.WebApi.Concurrency;

namespace Microsoft.Extensions.Hosting;

public static class SerginWebApiExtensions
{
    public static WebApplicationBuilder AddSerginWebApi(
        this WebApplicationBuilder builder,
        IReadOnlyCollection<ISerginModule> modules,
        IReadOnlyCollection<ISerginRemoteModule>? remoteModules = null)
    {
        builder.Services.AddOpenApi();

        IConfigurationSection serginSection =
            builder.Configuration.GetRequiredSection(SerginCoreExtensions.SectionName);

        SerginAuthOptions authOptions = builder.Services.AddSerginAuthOptions(serginSection);

        if (authOptions.Mode == SerginAuthMode.Keycloak)
        {
            // Registers IHttpContextAccessor and the claims-based user context itself.
            builder.Services.AddSerginKeycloakJwtBearer(authOptions);
        }
        else
        {
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddTransient<IUserContextFactory, InternalUserContextFactory>();
        }

        builder.AddSerginCore(modules, remoteModules);

        return builder;
    }

    public static async Task<WebApplication> UseSerginWebApiAsync(this WebApplication app, IReadOnlyCollection<ISerginModule> modules)
    {
        AggregateFeatureGuard.EnsureApplied(app.Services);

        // Builds the registry now, so a bad ICommandConfiguration fails start rather than the first send.
        CommandConfigurationRegistry registry = app.Services.GetRequiredService<CommandConfigurationRegistry>();
        app.Services.GetRequiredService<PermissionCatalog>();
        TextCatalog catalog = app.Services.GetRequiredService<TextCatalog>();

        EnsureCommandTypes(app.Services, registry);

        // Builds the scheduled command catalogue now, so a bad Schedulable() fails start.
        app.Services.GetRequiredService<ScheduledCommandCatalog>();

        // Every field a person sees has its label declared.
        app.Services.GetRequiredService<FieldTextGuard>().EnsureLabelled(registry, catalog);

        if (app.Environment.IsDevelopment())
        {
            foreach (ISerginModule module in modules)
            {
                await module.MigrateAsync(app.Services);
            }
        }

        if (app.Services.GetRequiredService<IOptions<SerginAuthOptions>>().Value.Mode == SerginAuthMode.Keycloak)
        {
            SerginAuthenticationExtensions.EnsureExternalIdentityResolverRegistered(app.Services);

            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.UseSerginApiRequestLocalization();

        foreach (ISerginWebApiModule webModule in modules.OfType<ISerginWebApiModule>())
        {
            webModule.MapEndpoints(app.MapGroup(webModule.Schema)
                .AddEndpointFilter<ExpectedVersionEndpointFilter>()
                .AddEndpointFilter<AuthorizationScopeEndpointFilter>());
        }

        app.MapOpenApi();

        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference();
        }

        return app;
    }

    /// <summary>
    /// The built-in request localization over the <c>Accept-Language</c> header, restricted to the default culture and
    /// the published languages: a draft is never served to an API caller. The language list is read live from the
    /// translation cache, and the chosen culture is loaded into it before the endpoint runs.
    /// </summary>
    public static IApplicationBuilder UseSerginApiRequestLocalization(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        ITranslationCache cache = app.ApplicationServices.GetRequiredService<ITranslationCache>();
        var defaultCulture = CultureInfo.GetCultureInfo(
            app.ApplicationServices.GetRequiredService<IOptions<SerginLocalizationOptions>>().Value.DefaultCulture);
        LanguageCultureList published = new(cache, defaultCulture, publishedOnly: true);

        RequestLocalizationOptions options = new()
        {
            DefaultRequestCulture = new RequestCulture(defaultCulture),
            SupportedCultures = published,
            SupportedUICultures = published,
            FallBackToParentCultures = false,
            FallBackToParentUICultures = false,
            RequestCultureProviders = [new PublishedAcceptLanguageProvider(cache, published)],
        };

        return app.UseRequestLocalization(options);
    }

    // Every typed record fits the command model, and every local one names an aggregate its module's DbContext maps.
    private static void EnsureCommandTypes(IServiceProvider services, CommandConfigurationRegistry registry)
    {
        services.GetRequiredService<CommandTypeGuard>().EnsureShaped(registry, services.GetServices<ModuleConfigurationSource>());
        CommandTargetMappingGuard.EnsureMapped(services.GetRequiredService<LocalContractsAssemblies>().RequestTypes(), services);
    }
}
