using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// A request a configuration made schedulable: its stable <paramref name="Name"/>, the module <paramref name="Schema"/>
/// that owns it (null for a source that is not a module's), the <paramref name="Root"/> it acts on, how it addresses a
/// target (<see cref="CommandTargetKind.Entity"/> by <c>Id</c>, <see cref="CommandTargetKind.ChildEntity"/> by
/// <c>RootId</c> and <c>Id</c>), the types of those ids, and whether it may run once per selected row.
/// </summary>
public sealed record ScheduledCommand(
    string Name,
    string? Schema,
    Type RequestType,
    Type Root,
    CommandTargetKind TargetKind,
    Type IdType,
    Type? RootIdType,
    bool Selectable);
