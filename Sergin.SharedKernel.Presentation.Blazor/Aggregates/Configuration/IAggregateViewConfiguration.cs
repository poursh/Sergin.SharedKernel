using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// The presentation facts of one root's generated views: its routes, titles, labels a command type cannot imply, field
/// overrides and slot components. Everything else (requests, permissions, version, schedule, field labels) is read
/// from the root's typed requests and their configurations. Declared once per root in the module's UI assembly, with a
/// parameterless constructor; <see cref="AggregateViewRegistry"/> runs <see cref="Configure"/> once at start.
/// </summary>
public interface IAggregateViewConfiguration<TRoot>
    where TRoot : class, IAggregateRoot
{
    void Configure(AggregateViewBuilder<TRoot> view);
}
