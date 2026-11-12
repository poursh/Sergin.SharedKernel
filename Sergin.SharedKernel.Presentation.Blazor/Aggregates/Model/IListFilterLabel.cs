namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// Optional on a configured filter component (<c>List(list =&gt; list.Filter(id, component))</c>): names the value it
/// picked, so the applied filter's chip reads <c>Scope: North</c> rather than the id.
/// </summary>
public interface IListFilterLabel
{
    /// <summary>What shows for <paramref name="value"/>, data as stored; null when the component does not know it.</summary>
    string? LabelOf(Guid value);
}
