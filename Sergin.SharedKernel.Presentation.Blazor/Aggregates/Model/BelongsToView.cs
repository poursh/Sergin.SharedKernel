namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Model;

/// <summary>
/// The parent record a root's record belongs to, from <c>BelongsTo&lt;TParent, TResponse&gt;(...)</c>: the parent root,
/// whose own model gives its list step and detail route, and the parent's id and name read from the loaded GetOne
/// response. A record with no parent (a top-level scope) answers a null id.
/// </summary>
/// <param name="Parent">The parent root.</param>
/// <param name="ParentId">Reads the parent's id from a GetOne response; null for a record with no parent.</param>
/// <param name="ParentName">Reads the parent's display name from a GetOne response, shown as stored; null when it has none.</param>
public sealed record BelongsToView(Type Parent, Func<object, Guid?> ParentId, Func<object, string?> ParentName);
