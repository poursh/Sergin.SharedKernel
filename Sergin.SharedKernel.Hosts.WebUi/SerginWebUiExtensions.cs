using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Hosts.Authentication;
using Sergin.SharedKernel.Hosts.WebUi;
using Sergin.SharedKernel.Hosts.WebUi.Users;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation;
using Sergin.SharedKernel.Presentation.Blazor.Home;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Security;

namespace Microsoft.Extensions.Hosting;

public static class SerginWebUiExtensions
{
    /// <param name="configureHome">
    /// Chooses what the site root renders. Omit it and the root shows <c>SerginWelcome</c> under a "Home"
    /// nav entry; supply it to swap in the application's own landing page, which is the seam that lets one
    /// codebase serve a device overview in one deployment and a dashboard in another.
    /// </param>
    /// <example>
    /// <code>
    /// builder.AddSerginBlazorApp(modules, configureHome: home => home.UseComponent&lt;MyDashboard&gt;());
    /// </code>
    /// </example>
    public static WebApplicationBuilder AddSerginBlazorApp(
        this WebApplicationBuilder builder,
        IReadOnlyCollection<ISerginModule> modules,
        IReadOnlyCollection<ISerginRemoteModule>? remoteModules = null,
        Action<SerginHomeBuilder>? configureHome = null)
    {
        IConfigurationSection serginSection =
            builder.Configuration.GetRequiredSection(SerginCoreExtensions.SectionName);

        SerginAuthOptions authOptions = builder.Services.AddSerginAuthOptions(serginSection);

        if (authOptions.Mode == SerginAuthMode.DevUser && !builder.Environment.IsDevelopment())
        {
            throw new InvalidOperationException(
                $"Sergin:{SerginAuthOptions.SectionName}:Mode is '{nameof(SerginAuthMode.DevUser)}', which means "
                + "no authentication at all: every request runs as the configured development user "
                + $"'Sergin:{DevUserOptions.SectionName}'. Refusing to start in the "
                + $"'{builder.Environment.EnvironmentName}' environment. Set Mode to "
                + $"'{nameof(SerginAuthMode.Keycloak)}' and configure the realm.");
        }

        builder.Services.AddRazorComponents().AddInteractiveServerComponents();

        builder.Services.AddSerginBlazorKit();

        if (authOptions.Mode == SerginAuthMode.Keycloak)
        {
            builder.Services.AddSerginKeycloakCookieOidc(authOptions);

            // Makes Task<AuthenticationState> available to any component that wants <AuthorizeView>.
            // Page-level gating is an endpoint concern instead — see UseSerginWebUiAsync — so that
            // static assets stay anonymous and the framework's own scripts load before sign-in.
            builder.Services.AddCascadingAuthenticationState();

            builder.Services.AddSingleton(new SerginUiAuthentication(
                CanSignOut: true,
                LogoutPath: $"{SerginAuthenticationExtensions.AuthenticationPath}/logout"));
        }
        else
        {
            // Only bound in DevUser mode: a Keycloak host has no Sergin:DevUser section to validate, and
            // ValidateOnStart would fail startup over keys nothing reads.
            builder.Services.AddOptions<DevUserOptions>()
                .Bind(serginSection.GetSection(DevUserOptions.SectionName))
                .ValidateOnStart();

            builder.Services.AddSingleton<IValidateOptions<DevUserOptions>, DevUserOptionsValidator>();

            builder.Services.AddTransient<IUserContextFactory, ConfiguredUserContextFactory>();

            builder.Services.AddSingleton(SerginUiAuthentication.Disabled);
        }

        // Bound against the whole Sergin section, not a subsection: ApplicationName is a scalar sitting
        // directly under it (Sergin:ApplicationName). Sibling keys the class has no property for are ignored.
        builder.Services.AddOptions<SerginApplicationOptions>()
            .Bind(serginSection)
            .ValidateOnStart();

        builder.Services
            .AddSingleton<IValidateOptions<SerginApplicationOptions>, SerginApplicationOptionsValidator>();

        builder.AddSerginCore(modules, remoteModules);

        builder.Services.AddSingleton(new SerginUiModuleCatalog([.. modules.OfType<ISerginWebUiModule>()]));

        SerginHomeBuilder homeBuilder = new();

        configureHome?.Invoke(homeBuilder);

        builder.Services.AddSingleton(homeBuilder.Build());

        return builder;
    }

    public static async Task<WebApplication> UseSerginWebUiAsync<TRootComponent>(
        this WebApplication app, IReadOnlyCollection<ISerginModule> modules)
        where TRootComponent : IComponent
    {
        SerginUiModuleCatalog catalog = app.Services.GetRequiredService<SerginUiModuleCatalog>();

        ValidateRoutePrefixes(catalog);

        if (app.Environment.IsDevelopment())
        {
            foreach (ISerginModule module in modules)
            {
                await module.MigrateAsync(app.Services);
            }
        }

        SerginAuthOptions authOptions = app.Services.GetRequiredService<IOptions<SerginAuthOptions>>().Value;
        bool keycloak = authOptions.Mode == SerginAuthMode.Keycloak;

        if (keycloak)
        {
            // Fail here rather than mid-callback on someone's first login attempt.
            SerginAuthenticationExtensions.EnsureExternalIdentityResolverRegistered(app.Services);

            app.UseAuthentication();
            app.UseAuthorization();
        }

        app.UseAntiforgery();

        app.MapStaticAssets();

        RazorComponentsEndpointConventionBuilder components = app.MapRazorComponents<TRootComponent>()
            .AddAdditionalAssemblies([.. catalog.RoutableAssemblies])
            .AddInteractiveServerRenderMode();

        if (keycloak)
        {
            // Authorization sits on the component endpoints, not on a global fallback policy: a fallback
            // would also gate MapStaticAssets, so the browser could not fetch the framework's own scripts
            // until after a sign-in those scripts are needed to complete.
            components.RequireAuthorization();

            app.MapSerginLoginAndLogout();
        }

        return app;
    }

    private static void ValidateRoutePrefixes(SerginUiModuleCatalog catalog)
    {
        List<string> violations = [];

        foreach (ISerginWebUiModule module in catalog.Modules)
        {
            string prefix = $"/{module.Schema}/";

            foreach (Type component in module.UiAssembly.GetExportedTypes())
            {
                if (!typeof(IComponent).IsAssignableFrom(component))
                {
                    continue;
                }

                foreach (RouteAttribute route in component.GetCustomAttributes<RouteAttribute>(inherit: false))
                {
                    if (!route.Template.StartsWith(prefix, StringComparison.Ordinal))
                    {
                        violations.Add($"  {component.FullName}: @page \"{route.Template}\" must start with \"{prefix}\"");
                    }
                }
            }
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Module routable components must sit under their module's schema prefix, because Razor @page "
                + "templates are compile-time constants and cannot be prefixed at map time the way "
                + "MapGroup(schema) prefixes minimal-API endpoints:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, violations));
        }
    }
}
