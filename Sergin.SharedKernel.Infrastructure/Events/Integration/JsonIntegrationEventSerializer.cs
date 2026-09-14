using System.Text.Json;
using Sergin.SharedKernel.Application.Events.Integration;

namespace Sergin.SharedKernel.Infrastructure.Events.Integration;

/// <summary>
/// The outbox's wire format: <see cref="System.Text.Json"/> with web defaults (camelCase), since every
/// integration event is a plain, primitive-only positional record with nothing that needs a custom
/// converter. Deserializing resolves the CLR type through <see cref="IIntegrationEventTypeRegistry"/> rather
/// than a type discriminator embedded in the JSON itself, so the stored content never carries a CLR type
/// name — only <see cref="IntegrationEventNameAttribute"/>'s stable name does, alongside it in the row.
/// </summary>
internal sealed class JsonIntegrationEventSerializer(IIntegrationEventTypeRegistry registry) : IIntegrationEventSerializer
{
    private static readonly JsonSerializerOptions options = new(JsonSerializerDefaults.Web);

    public string Serialize(IIntegrationEvent integrationEvent) =>
        JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType(), options);

    public IIntegrationEvent Deserialize(string typeName, string content) =>
        JsonSerializer.Deserialize(content, registry.TypeOf(typeName), options) is IIntegrationEvent integrationEvent
            ? integrationEvent
            : throw new InvalidOperationException($"Deserializing '{typeName}' produced a null result.");
}
