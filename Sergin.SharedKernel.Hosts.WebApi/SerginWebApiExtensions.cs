using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Hosts.Authentication;
using Sergin.SharedKernel.Infrastracture.WebApi.Users;
using Sergin.SharedKernel.Modules;

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

        foreach (ISerginWebApiModule webModule in modules.OfType<ISerginWebApiModule>())
        {
            webModule.MapEndpoints(app.MapGroup(webModule.Schema));
        }

        app.MapOpenApi();

        if (app.Environment.IsDevelopment())
        {
            app.MapScalarApiReference();
        }

        return app;
    }
}
