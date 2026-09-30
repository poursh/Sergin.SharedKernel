namespace Sergin.SharedKernel.Application.Concurrency;

/// <summary>
/// Carries a request's aggregate version beside the request, never on it. A front end sets
/// <see cref="Expected"/> before the send (the Blazor dispatcher, the WebApi If-Match filter, the gRPC server
/// interceptor) and reads <see cref="Current"/> after it. During the send, a GetOne handler sets
/// <see cref="Current"/> from the row it read, and the row-version interceptor sets it to the version it wrote.
/// <para>
/// Registered scoped, next to <see cref="Securities.Users.UserContextAccessor"/> and for the same reason: each
/// Blazor send and each gRPC call runs in its own scope, so a value never crosses requests.
/// </para>
/// </summary>
public sealed class ConcurrencyContext
{
    /// <summary>The version the caller last saw. Null means the caller sent none.</summary>
    public RowVersion? Expected { get; set; }

    /// <summary>The aggregate's version after the send: read by a GetOne, or written by a save.</summary>
    public RowVersion? Current { get; set; }
}
