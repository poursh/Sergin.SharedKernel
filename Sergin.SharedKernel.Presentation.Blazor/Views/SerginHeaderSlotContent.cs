using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>Writes its child content into a <see cref="SerginHeaderSlot"/> and renders nothing where it stands.</summary>
public sealed class SerginHeaderSlotContent : ComponentBase, IDisposable
{
    [Parameter]
    [EditorRequired]
    public SerginHeaderSlot Slot { get; set; } = default!;

    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    public void Dispose() => Slot.Clear(this);

    // Every render of the view hands a new fragment, so the outlets re-render with the view's current state.
    protected override void OnParametersSet() => Slot.Set(this, ChildContent);
}
