using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>The end of an activity row: Cancel while the run is running, or a note that its task cannot be cancelled.</summary>
public sealed partial class SerginActivityRowAction
{
    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Parameter]
    public bool Running { get; set; }

    [Parameter]
    public bool Cancelable { get; set; } = true;

    [Parameter]
    public EventCallback OnCancel { get; set; }

    private string CancelLabel => Localizer.Text(TaskUiTexts.Activity.Cancel);
}
