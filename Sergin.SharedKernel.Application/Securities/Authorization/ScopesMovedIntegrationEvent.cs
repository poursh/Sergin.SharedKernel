using Sergin.SharedKernel.Application.Events.Integration;

namespace Sergin.SharedKernel.Application.Securities.Authorization;

/// <summary>
/// Scopes whose path changed: a moved scope and every scope below it. Published by the module that owns the scope
/// tree; each module with scoped aggregates rewrites the stored path of every record in those scopes. It carries the
/// ids only, never the new paths, so a consumer always writes the path as it is now: delivering it twice, or after a
/// later move, gives the same result.
/// </summary>
[IntegrationEventName("sk.scopes.moved.v1")]
public sealed record ScopesMovedIntegrationEvent(Guid Id, DateTime OccurredOnUtc, IReadOnlyCollection<Guid> ScopeIds)
    : IIntegrationEvent;
