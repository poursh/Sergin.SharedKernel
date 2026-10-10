namespace Sergin.SharedKernel.Presentation.Grpc.Authorization;

/// <summary>
/// The gRPC metadata key carrying <see cref="Application.Securities.Authorization.IAuthorizationScopeContext"/>
/// across a Remote call, as a request header. Absent means the root scope.
/// </summary>
public static class AuthorizationScopeMetadata
{
    public const string ScopeKey = "sergin-scope";
}
