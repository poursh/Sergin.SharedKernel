using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Infrastracture.WebApi.Users;
using Sergin.SharedKernel.Modules;

namespace Microsoft.Extensions.Hosting;

public static class SerginWebApiExtensions
{
    public static WebApplicationBuilder AddSerginWebApi(this WebApplicationBuilder builder, IReadOnlyCollection<ISerginModule> modules)
    {
        builder.Services.AddOpenApi();

        builder.Services.AddHttpContextAccessor();
        builder.Services.AddTransient<IUserContextFactory, InternalUserContextFactory>();

        builder.AddSerginCore(modules);

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
