using Grpc.Core;
using Grpc.Core.Interceptors;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Grpc.Concurrency;

/// <summary>
/// The serving host's half: seeds the call scope's <see cref="ConcurrencyContext.Expected"/> from request
/// metadata before the service sends into its pipeline, and returns <see cref="ConcurrencyContext.Current"/>
/// as a trailer. Register with <c>AddGrpc(o =&gt; o.Interceptors.Add&lt;ConcurrencyServerInterceptor&gt;())</c>;
/// Grpc.AspNetCore builds it from the call's scope, so it shares the context the service's ISender uses.
/// </summary>
public sealed class ConcurrencyServerInterceptor(ConcurrencyContext concurrency) : Interceptor
{
    public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(
        TRequest request,
        ServerCallContext context,
        UnaryServerMethod<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(continuation);

        if (context.RequestHeaders.GetValue(ConcurrencyMetadata.ExpectedVersionKey) is { } sent)
        {
            concurrency.Expected = Guid.TryParse(sent, out Guid expected) && expected != Guid.Empty
                ? RowVersion.Create(expected)
                : throw new RpcException(new Status(
                    StatusCode.InvalidArgument,
                    $"{ConcurrencyMetadata.ExpectedVersionKey} must be a non-empty GUID."));
        }

        TResponse response = await continuation(request, context).ConfigureAwait(false);

        if (concurrency.Current is { } current)
        {
            context.ResponseTrailers.Add(ConcurrencyMetadata.VersionKey, current.Value.ToString());
        }

        return response;
    }
}
