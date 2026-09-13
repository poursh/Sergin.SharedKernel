namespace Sergin.SharedKernel.Application.Events.Integration;

/// <summary>
/// Turns an integration event into the outbox's <c>content</c> column and back. Declared here, in
/// <c>.Application</c>, rather than alongside the JSON implementation in <c>.Infrastructure</c>, so both the
/// outbox writer (<c>.Infrastructure.Data.EFCore</c>, which writes <c>Serialize</c>'s result to a row) and the
/// relay (which calls <c>Deserialize</c> before dispatching) depend on this contract only — neither needs a
/// reference to how the wire format is actually produced.
/// </summary>
public interface IIntegrationEventSerializer
{
    string Serialize(IIntegrationEvent integrationEvent);

    IIntegrationEvent Deserialize(string typeName, string content);
}
