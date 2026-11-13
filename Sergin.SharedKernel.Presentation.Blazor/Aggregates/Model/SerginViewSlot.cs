namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>A place in a generated view where a configuration may put its own component.</summary>
public enum SerginViewSlot
{
    /// <summary>Replaces the generated fields of the detail view.</summary>
    DetailFields,

    /// <summary>Rendered below the detail view's fields, where entity sections go.</summary>
    DetailEntities,

    /// <summary>The detail view's overview figures (the entity overview, in <c>Context.Overview</c>).</summary>
    DetailOverview,

    /// <summary>The detail view's overview line when collapsed.</summary>
    DetailOverviewSummary,

    /// <summary>The list view's overview figures.</summary>
    ListOverview,

    /// <summary>The list view's overview line when collapsed.</summary>
    ListOverviewSummary,
}
