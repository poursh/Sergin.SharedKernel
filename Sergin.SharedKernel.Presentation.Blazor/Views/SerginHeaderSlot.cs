using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>
/// Where a <see cref="SerginViewHostKind.Pane"/> view's header buttons go: the view writes them through
/// <see cref="SerginHeaderSlotContent"/>, and every <see cref="SerginHeaderSlotOutlet"/> on the slot renders them.
/// </summary>
/// <remarks>
/// Blazor's sections take one outlet per id, and the page header renders its buttons twice — in a row from md up and
/// folded into a menu below it — so a pane's buttons need a slot that more than one outlet can read. Only the
/// content that wrote last can clear it: on a navigation the next page's view writes before the old one is disposed.
/// </remarks>
public sealed class SerginHeaderSlot
{
    private object? owner;

    /// <summary>Raised when the content changes; an outlet renders again.</summary>
    public event EventHandler? Changed;

    /// <summary>The buttons to render; null while no view writes any.</summary>
    public RenderFragment? Content { get; private set; }

    internal void Set(object writer, RenderFragment? content)
    {
        owner = writer;
        Content = content;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    internal void Clear(object writer)
    {
        if (!ReferenceEquals(owner, writer))
        {
            return;
        }

        owner = null;
        Content = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}
