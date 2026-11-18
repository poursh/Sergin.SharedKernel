using MediatR;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// Checks every permission a request's configuration requires on the scope <see cref="IAuthorizationScopeContext"/>
/// holds, or, for a request whose type names an aggregate that is not scoped (<see cref="PermissionScope.Any"/>), on any
/// scope: the caller only has to hold each permission somewhere. Once the check passes, the request runs
/// with two things set for it: its permissions in <see cref="RequiredPermissionsContext"/>, which the save checks
/// again on the scope of every record it writes, and <see cref="ScopeFilter"/>, which narrows its reads of scoped
/// aggregates to the current subtree and to where the caller holds every one of those permissions. The save's
/// <see cref="ForbiddenException"/> answers <c>Error.Forbidden()</c> like a refusal here.
/// </summary>
internal sealed class PermissionCheckPipelineBehavior<TRequest, TResponse>(
    RequestPermissionGate gate,
    CommandConfigurationRegistry configurations) : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IBaseCommand
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
    {
        CommandSettings settings = configurations.For(request.GetType());

        // An unconfigured request runs with nothing published: no record check on its save, and the scope filter's
        // fail-closed default for its reads.
        if (settings.RequiredPermissions.Count == 0)
        {
            return await next(cancellationToken);
        }

        // The check, the published permissions and the narrowed reads are the gate's (see RequestPermissionGate).
        using IDisposable? entered = await gate.EnterAsync(settings, cancellationToken);

        if (entered is null)
        {
            return Forbidden();
        }

        TResponse forbidden = default!;

        try
        {
            return await next(cancellationToken);
        }
        catch (ForbiddenException) when (ErrorOrResponse.TryFrom(Error.Forbidden(), out forbidden))
        {
            return forbidden;
        }
    }

    private static TResponse Forbidden() =>
        ErrorOrResponse.TryFrom(Error.Forbidden(), out TResponse forbidden) ? forbidden : throw new ForbiddenException();
}
