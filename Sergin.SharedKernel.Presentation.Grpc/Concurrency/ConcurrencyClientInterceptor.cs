using Grpc.Core;
using Grpc.Core.Interceptors;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Grpc.Concurrency;

/// <summary>
/// The caller's half of carrying a version over a Remote call. Put on the channel a module's invoker calls
/// through (<c>channel.Intercept(new ConcurrencyClientInterceptor(concurrency))</c>, with the caller scope's
/// context), so no <see cref="Dispatching.IRemoteInvoker{TRequest, TResponse}"/> changes: it sends
/// <see cref="ConcurrencyContext.Expected"/> as request metadata and copies the version trailer back into
/// <see cref="ConcurrencyContext.Current"/>.
/// </summary>
public sealed class ConcurrencyClientInterceptor(ConcurrencyContext concurrency) : Interceptor
{
    public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
        TRequest request,
        ClientInterceptorContext<TRequest, TResponse> context,
        AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
    {
        ArgumentNullException.ThrowIfNull(continuation);

        // A new Metadata, never the caller's own instance: context.Options.Headers may be one the caller built
        // and still holds a reference to, and CallOptions is meant to be immutable per call.
        Metadata headers = [];

        if (context.Options.Headers is { } existing)
        {
            foreach (Metadata.Entry entry in existing.Where(entry => entry.Key != ConcurrencyMetadata.ExpectedVersionKey))
            {
                headers.Add(entry);
            }
        }

        if (concurrency.Expected is { } expected)
        {
            headers.Add(ConcurrencyMetadata.ExpectedVersionKey, expected.Value.ToString());
        }

        AsyncUnaryCall<TResponse> call = continuation(
            request,
            new ClientInterceptorContext<TRequest, TResponse>(context.Method, context.Host, context.Options.WithHeaders(headers)));

        return new AsyncUnaryCall<TResponse>(
            ReadVersionAsync(call),
            call.ResponseHeadersAsync,
            call.GetStatus,
            call.GetTrailers,
            call.Dispose);
    }

    private async Task<TResponse> ReadVersionAsync<TResponse>(AsyncUnaryCall<TResponse> call)
    {
        TResponse response = await call.ResponseAsync.ConfigureAwait(false);

        if (Guid.TryParse(call.GetTrailers().GetValue(ConcurrencyMetadata.VersionKey), out Guid version) && version != Guid.Empty)
        {
            concurrency.Current = RowVersion.Create(version);
        }

        return response;
    }
}
