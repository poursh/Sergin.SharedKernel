using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

/// <summary>
/// A send's result together with the aggregate version it read or wrote. <see cref="Version"/> is null when the
/// send failed, or touched no versioned aggregate.
/// </summary>
public sealed record VersionedResult<TResponse>(ErrorOr<TResponse> Result, RowVersion? Version);
