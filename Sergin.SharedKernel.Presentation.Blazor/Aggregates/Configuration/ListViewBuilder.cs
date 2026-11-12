using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>Overrides for the list item's fields, plus filter pickers the list cannot generate.</summary>
public sealed class ListViewBuilder<TItem> : FieldSetBuilder<TItem>, IListFilterDeclarations
{
    internal ListViewBuilder()
    {
    }

    /// <summary>The filter pickers the configuration supplied, by list field id.</summary>
    internal Dictionary<string, Type> FilterComponents { get; } = new(StringComparer.Ordinal);

    IReadOnlyDictionary<string, Type> IListFilterDeclarations.FilterComponents => FilterComponents;

    /// <summary>
    /// Replaces the generated picker of the <c>Identifier</c> filter <paramref name="listFieldId"/> with
    /// <paramref name="component"/>, which takes <c>[Parameter] Guid? Value</c> and <c>EventCallback&lt;Guid?&gt; ValueChanged</c>.
    /// </summary>
    public ListViewBuilder<TItem> Filter(string listFieldId, Type component)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(listFieldId);
        ArgumentNullException.ThrowIfNull(component);

        if (!typeof(IComponent).IsAssignableFrom(component))
        {
            throw new InvalidOperationException(
                $"Filter(\"{listFieldId}\", {component.FullName}) on {typeof(TItem).FullName}: {component.Name} is not a component.");
        }

        FilterComponents[listFieldId] = component;
        return this;
    }
}

/// <summary>What a list builder of any item type collected, read by the registry.</summary>
internal interface IListFilterDeclarations
{
    IReadOnlyDictionary<string, Type> FilterComponents { get; }
}
