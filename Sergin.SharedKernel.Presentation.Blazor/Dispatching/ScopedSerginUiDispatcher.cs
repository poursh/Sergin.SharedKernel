using Microsoft.Extensions.DependencyInjection;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

internal sealed class ScopedSerginUiDispatcher(IServiceScopeFactory scopeFactory) : ISerginUiDispatcher
{
    public async Task<ErrorOr<TResponse>> SendAsync<TResponse>(
        IRequest<ErrorOr<TResponse>> request, CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        ISender sender = scope.ServiceProvider.GetRequiredService<ISender>();

        return await sender.Send(request, cancellationToken);
    }
}
