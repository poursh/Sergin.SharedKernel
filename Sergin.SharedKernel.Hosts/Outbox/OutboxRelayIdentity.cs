using Sergin.SharedKernel.Application.Events.Integration;
using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Hosts.Outbox;

/// <summary>
/// The host's answer to <see cref="IOutboxRelayIdentity"/>: one fixed <see cref="RelayUserContext"/>, held
/// for the life of the host. Lives here rather than next to the relay because what "the relay's identity"
/// should be is a host decision, the same way the host decides which <c>IUserContextFactory</c> serves
/// interactive callers — the relay itself only ever asks for <see cref="User"/>.
/// </summary>
internal sealed class OutboxRelayIdentity : IOutboxRelayIdentity
{
    public IUserContext User { get; } = new RelayUserContext();
}
