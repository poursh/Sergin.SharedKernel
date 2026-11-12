using System.Reflection;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Sends one request type through <see cref="ISerginDispatcher"/> without naming its response type: the closed generic
/// <c>SendAsync&lt;TResponse&gt;</c> / <c>SendVersionedAsync&lt;TResponse&gt;</c> delegates are made once, so nothing
/// is reflected per send.
/// </summary>
public sealed class RequestInvoker
{
    private static readonly MethodInfo sendCore = typeof(Core).GetMethod(nameof(Core.SendCore))!;
    private static readonly MethodInfo sendVersionedCore = typeof(Core).GetMethod(nameof(Core.SendVersionedCore))!;

    private readonly Func<ISerginDispatcher, object, CancellationToken, Task<ViewSendResult>> send;
    private readonly Func<ISerginDispatcher, object, RowVersion?, CancellationToken, Task<ViewSendResult>> sendVersioned;

    private RequestInvoker(
        Type requestType,
        Func<ISerginDispatcher, object, CancellationToken, Task<ViewSendResult>> send,
        Func<ISerginDispatcher, object, RowVersion?, CancellationToken, Task<ViewSendResult>> sendVersioned)
    {
        RequestType = requestType;
        this.send = send;
        this.sendVersioned = sendVersioned;
    }

    public Type RequestType { get; }

    /// <summary>The invoker of <paramref name="requestType"/>, which must be an <c>IRequest&lt;ErrorOr&lt;T&gt;&gt;</c>.</summary>
    public static RequestInvoker For(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        Type response = ResponseOf(requestType)
            ?? throw new InvalidOperationException($"{requestType.FullName} is not an IRequest<ErrorOr<T>>, so a view cannot send it.");

        return new RequestInvoker(
            requestType,
            sendCore.MakeGenericMethod(response).CreateDelegate<Func<ISerginDispatcher, object, CancellationToken, Task<ViewSendResult>>>(),
            sendVersionedCore.MakeGenericMethod(response).CreateDelegate<Func<ISerginDispatcher, object, RowVersion?, CancellationToken, Task<ViewSendResult>>>());
    }

    public Task<ViewSendResult> SendAsync(ISerginDispatcher dispatcher, object request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(request);

        return send(dispatcher, request, cancellationToken);
    }

    public Task<ViewSendResult> SendVersionedAsync(
        ISerginDispatcher dispatcher, object request, RowVersion? expected, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);
        ArgumentNullException.ThrowIfNull(request);

        return sendVersioned(dispatcher, request, expected, cancellationToken);
    }

    private static Type? ResponseOf(Type requestType) =>
        requestType.GetInterfaces()
            .Where(candidate => candidate.IsGenericType && candidate.GetGenericTypeDefinition() == typeof(IRequest<>))
            .Select(candidate => candidate.GetGenericArguments()[0])
            .Where(result => result.IsGenericType && result.GetGenericTypeDefinition() == typeof(ErrorOr<>))
            .Select(result => result.GetGenericArguments()[0])
            .FirstOrDefault();

    private static class Core
    {
        public static async Task<ViewSendResult> SendCore<TResponse>(ISerginDispatcher dispatcher, object request, CancellationToken cancellationToken)
        {
            ErrorOr<TResponse> result = await dispatcher.SendAsync((IRequest<ErrorOr<TResponse>>)request, cancellationToken);

            return result.IsError ? new ViewSendResult(null, null, result.Errors) : new ViewSendResult(result.Value, null, []);
        }

        public static async Task<ViewSendResult> SendVersionedCore<TResponse>(
            ISerginDispatcher dispatcher, object request, RowVersion? expected, CancellationToken cancellationToken)
        {
            ErrorOr<Versioned<TResponse>> result = await dispatcher.SendVersionedAsync((IRequest<ErrorOr<TResponse>>)request, expected, cancellationToken);

            return result.IsError ? new ViewSendResult(null, null, result.Errors) : new ViewSendResult(result.Value.Value, result.Value.Version, []);
        }
    }
}
