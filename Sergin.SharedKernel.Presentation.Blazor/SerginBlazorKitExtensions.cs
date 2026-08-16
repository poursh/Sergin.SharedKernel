using MudBlazor.Services;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;

namespace Microsoft.Extensions.DependencyInjection;

public static class SerginBlazorKitExtensions
{
    public static IServiceCollection AddSerginBlazorKit(this IServiceCollection services)
    {
        services.AddMudServices();

        services.AddSingleton<ISerginUiDispatcher, ScopedSerginUiDispatcher>();
        services.AddScoped<IUiErrorPresenter, MudUiErrorPresenter>();

        return services;
    }
}
