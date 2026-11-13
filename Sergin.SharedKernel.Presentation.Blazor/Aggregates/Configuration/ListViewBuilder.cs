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

    /// <summary>The quick-search placeholder the configuration gave, or null for the shell's.</summary>
    internal TextDefinition? DeclaredSearchPlaceholder { get; private set; }

    TextDefinition? IListFilterDeclarations.SearchPlaceholder => DeclaredSearchPlaceholder;

    /// <summary>The quick search's placeholder, naming what it searches, replacing the shell's <c>Quick search…</c>.</summary>
    public ListViewBuilder<TItem> SearchPlaceholder(TextDefinition placeholder)
    {
        ArgumentNullException.ThrowIfNull(placeholder);
        DeclaredSearchPlaceholder = placeholder;
        return this;
    }

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

    TextDefinition? SearchPlaceholder { get; }
}
