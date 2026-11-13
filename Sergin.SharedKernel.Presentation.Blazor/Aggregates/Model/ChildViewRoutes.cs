namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Where a child entity's generated pages live; the first <see cref="Guid"/> of each is always the root's id. Each must
/// match a routable component's <c>[Route]</c> template; the start guard checks it.
/// </summary>
public sealed record ChildViewRoutes(Func<Guid, Guid, string>? Detail, Func<Guid, string>? Create, Func<Guid, Guid, string>? Edit)
{
    /// <summary>No routes: the child's views are only embedded.</summary>
    public static ChildViewRoutes None { get; } = new(Detail: null, Create: null, Edit: null);

    /// <summary>Whether any route is configured.</summary>
    public bool Any => Detail is not null || Create is not null || Edit is not null;
}
