using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// What a generated view hands a configured component or menu item: the loaded record (null on a list), the version it
/// was loaded at, a way to reload the view, and the ids the action targets (the record's, or the selected rows'), and the
/// overview the view loaded once for its slots (the entity overview on a detail, the collection overview on a list; null
/// with none or when its read failed). A list under a parent also carries the parent's id and its GetOne response (null
/// when its read failed), and every list carries a generation the header's Reload bumps, so a list body slot knows to
/// reload its rows.
/// </summary>
public sealed record AggregateViewContext(
    object? Record,
    RowVersion? Version,
    Func<Task> ReloadAsync,
    IReadOnlyList<Guid> TargetIds,
    object? Overview = null,
    Guid? ParentId = null,
    object? Parent = null,
    int Generation = 0);
