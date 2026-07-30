using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Events;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Infrastracture.Data;
using Sergin.SharedKernel.Infrastracture.WebApi.Users;
using Sergin.SharedKernel.Infrastructure.Data.EFCore;
using Sergin.SharedKernel.Infrastructure.Data.EFCore.Interceptors;
using Sergin.SharedKernel.Infrastructure.Events;
using Sergin.SharedKernel.Infrastructure.Localizations;
using Sergin.SharedKernel.Modules;

namespace Microsoft.Extensions.Hosting;

public static class SerginWebApiExtensions
{
    public static WebApplicationBuilder AddSerginWebApi(this WebApplicationBuilder builder, IReadOnlyCollection<ISerginModule> modules)
    {
        IConfigurationSection serginSection = builder.Configuration.GetRequiredSection("Sergin");

        builder.Services.AddMediatR(options =>
        {
            foreach (ISerginModule module in modules)
            {
                options.RegisterServicesFromAssembly(module.ApplicationAssembly);
            }

            options.AddOpenBehavior(typeof(PermissionCheckPipelineBehavior<,>));
            options.AddOpenBehavior(typeof(ValidationPipelineBehavior<,>));
        });

        builder.Services.AddOpenApi();

        builder.Services.AddScoped<IEventDispatcher, DefaultEventDispatcher>();
        builder.Services.AddScoped<EventDispatcherInterceptor>();

        string connectionString = serginSection.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Sergin:ConnectionStrings:Database' is not configured.");

        builder.Services.AddScoped<IDbConnectionFactory>(p => new PostgresDbConnectionFactory(connectionString));

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<IUserContextFactory, InternalUserContextFactory>();
        builder.Services.AddScoped(p => p.GetRequiredService<IUserContextFactory>().CreateUserContext());

        builder.Services.AddSingleton<ILocalizer, DefaultLocalizer>();

        foreach (ISerginModule module in modules)
        {
            module.AddServices(builder.Services, serginSection);
        }

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
