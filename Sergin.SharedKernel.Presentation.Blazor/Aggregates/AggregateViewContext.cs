using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// What a generated view hands a configured component or menu item: the loaded record (null on a list), the version it
/// was loaded at, a way to reload the view, and the ids the action targets (the record's, or the selected rows').
/// </summary>
public sealed record AggregateViewContext(object? Record, RowVersion? Version, Func<Task> ReloadAsync, IReadOnlyList<Guid> TargetIds);
