using Sergin.SharedKernel.Application.Securities.Users;

namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// The identity the in-process dispatcher stamps onto the consumer scope it opens per message, before
/// publishing, so a consumer handler's <c>ISender.Send(command)</c> runs as a real, permission-holding user
/// instead of whatever a background scope would otherwise resolve. Kept as a contract here so the host — the
/// only layer that knows what "the relay's identity" should actually be — supplies the implementation, while
/// the dispatcher itself depends on nothing more than this interface.
/// </summary>
public interface IOutboxRelayIdentity
{
    IUserContext User { get; }
}
