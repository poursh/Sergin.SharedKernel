using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;

namespace Sergin.SharedKernel.Presentation.Blazor.Views;

/// <summary>Renders what a <see cref="SerginHeaderSlot"/> holds, and renders again when it changes.</summary>
public sealed class SerginHeaderSlotOutlet : ComponentBase, IDisposable
{
    [Parameter]
    [EditorRequired]
    public SerginHeaderSlot Slot { get; set; } = default!;

    public void Dispose() => Slot.Changed -= OnChanged;

    protected override void OnInitialized() => Slot.Changed += OnChanged;

    protected override void BuildRenderTree(RenderTreeBuilder builder) => builder.AddContent(0, Slot.Content);

    private void OnChanged(object? sender, EventArgs args) => _ = InvokeAsync(StateHasChanged);
}
