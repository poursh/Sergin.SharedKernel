using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// A request a configuration made schedulable: its stable <paramref name="Name"/>, the module <paramref name="Schema"/>
/// that owns it (null for a source that is not a module's), the <paramref name="Root"/> it acts on, how it addresses a
/// target (<see cref="CommandTargetKind.Entity"/> by <c>Id</c>, <see cref="CommandTargetKind.ChildEntity"/> by
/// <c>RootId</c> and <c>Id</c>), the types of those ids, whether it may run once per selected row, and its <paramref name="Background"/> declaration (null when it may run in the foreground).
/// </summary>
public sealed record TaskDescriptor(
    string Name,
    string? Schema,
    Type RequestType,
    Type Root,
    CommandTargetKind TargetKind,
    Type IdType,
    Type? RootIdType,
    bool Selectable,
    BackgroundSettings? Background = null);
