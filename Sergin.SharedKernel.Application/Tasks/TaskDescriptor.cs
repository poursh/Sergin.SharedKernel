using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// A task (<see cref="ITaskCommand"/>) as the catalogue holds it: its stable <paramref name="Name"/>, the module
/// <paramref name="Schema"/> that owns it (null for a source that is not a module's), the <paramref name="Root"/> it acts
/// on, how it addresses a target (<see cref="CommandTargetKind.Entity"/> by <c>Id</c>, of <paramref name="IdType"/>; a
/// collection task names no record, so its <paramref name="IdType"/> is null), whether it may run once per selected row,
/// and what its configuration declared (<paramref name="Settings"/>).
/// </summary>
public sealed record TaskDescriptor(
    string Name,
    string? Schema,
    Type RequestType,
    Type Root,
    CommandTargetKind TargetKind,
    Type? IdType,
    bool Selectable,
    TaskSettings Settings);
