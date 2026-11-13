using System.Reflection;
using Sergin.SharedKernel.Application.Concurrency;
using Sergin.SharedKernel.Domain;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Validation;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Sends one request type through <see cref="ISerginDispatcher"/> without naming its response type: the closed generic
/// <c>SendAsync&lt;TResponse&gt;</c> / <c>SendVersionedAsync&lt;TResponse&gt;</c> delegates are made once, so nothing
/// is reflected per send. <see cref="ValidateAsync"/> runs <see cref="ISerginFormValidator"/> for the request type the
/// same way, so a generated form's fields are checked by the pipeline's own validator of the closed type.
/// </summary>
public sealed class RequestInvoker
{
    private static readonly MethodInfo sendCore = typeof(Core).GetMethod(nameof(Core.SendCore))!;
    private static readonly MethodInfo sendVersionedCore = typeof(Core).GetMethod(nameof(Core.SendVersionedCore))!;
    private static readonly MethodInfo validateCore = typeof(Core).GetMethod(nameof(Core.ValidateCore))!;

    private readonly Func<ISerginDispatcher, object, CancellationToken, Task<ViewSendResult>> send;
    private readonly Func<ISerginDispatcher, object, RowVersion?, CancellationToken, Task<ViewSendResult>> sendVersioned;
    private readonly Func<ISerginFormValidator, object, string, CancellationToken, Task<IReadOnlyCollection<string>>> validate;

    private RequestInvoker(
        Type requestType,
        Func<ISerginDispatcher, object, CancellationToken, Task<ViewSendResult>> send,
        Func<ISerginDispatcher, object, RowVersion?, CancellationToken, Task<ViewSendResult>> sendVersioned,
        Func<ISerginFormValidator, object, string, CancellationToken, Task<IReadOnlyCollection<string>>> validate)
    {
        RequestType = requestType;
        this.send = send;
        this.sendVersioned = sendVersioned;
        this.validate = validate;
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
            sendVersionedCore.MakeGenericMethod(response).CreateDelegate<Func<ISerginDispatcher, object, RowVersion?, CancellationToken, Task<ViewSendResult>>>(),
            validateCore.MakeGenericMethod(requestType)
                .CreateDelegate<Func<ISerginFormValidator, object, string, CancellationToken, Task<IReadOnlyCollection<string>>>>());
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

    /// <summary>
    /// The messages of the pipeline validator's rules for <paramref name="propertyName"/> of <paramref name="request"/>
    /// (<see cref="ISerginFormValidator.ValidateAsync{TRequest}"/> closed over this request type).
    /// </summary>
    public Task<IReadOnlyCollection<string>> ValidateAsync(
        ISerginFormValidator validator, object request, string propertyName, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(propertyName);

        return validate(validator, request, propertyName, cancellationToken);
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

        public static Task<IReadOnlyCollection<string>> ValidateCore<TRequest>(
            ISerginFormValidator validator, object request, string propertyName, CancellationToken cancellationToken) =>
            validator.ValidateAsync((TRequest)request, propertyName, cancellationToken);
    }
}
