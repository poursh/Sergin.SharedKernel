namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Where a root's generated pages live. The detail and edit routes take the record's id. Each must match a routable
/// component's <c>[Route]</c> template; the start guard checks it. A root listed under a parent has no
/// <paramref name="List"/>: its list route is <paramref name="ListUnderParent"/>, built from the parent's id.
/// </summary>
public sealed record AggregateViewRoutes(
    string? List,
    Func<Guid, string>? Detail,
    string? Create,
    Func<Guid, string>? Edit,
    Func<Guid, string>? ListUnderParent = null)
{
    /// <summary>No routes: the root's views are only embedded.</summary>
    public static AggregateViewRoutes None { get; } = new(List: null, Detail: null, Create: null, Edit: null);

    /// <summary>Whether any route is configured.</summary>
    public bool Any => List is not null || Detail is not null || Create is not null || Edit is not null || ListUnderParent is not null;
}
