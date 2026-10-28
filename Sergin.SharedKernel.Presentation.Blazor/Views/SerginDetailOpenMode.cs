namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>How a <see cref="SerginEntityListView"/>'s rows open an entity's detail view.</summary>
/// <remarks>
/// Only <see cref="Page"/> exists today. A modal mode arrives together with the dialog host that can show a view, so
/// there is never a value that does nothing.
/// </remarks>
public enum SerginDetailOpenMode
{
    /// <summary>The link navigates to the entity's detail page.</summary>
    Page,
}
