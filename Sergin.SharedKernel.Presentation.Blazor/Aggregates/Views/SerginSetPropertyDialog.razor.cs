using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The Dialog edit mode of a root's set-property command (the default): the command's label as title, its value form
/// prefilled from the record, Save and Cancel. Save hands the value to <see cref="Submit"/>, the view's send; the dialog
/// closes when it answers true (sent) and stays open with the user's value otherwise.
/// </summary>
public sealed partial class SerginSetPropertyDialog
{
    private bool busy;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public CommandView Command { get; set; } = default!;

    [Parameter]
    public Guid Id { get; set; }

    /// <summary>The record's value of the command's property, or null when it has none.</summary>
    [Parameter]
    public object? Value { get; set; }

    /// <summary>Sends the value, shows any errors and reloads the record; true when it was sent.</summary>
    [Parameter]
    [EditorRequired]
    public Func<object?, Task<bool>> Submit { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private EventCallback<object?> OnSubmit => EventCallback.Factory.Create<object?>(this, SubmitAsync);

    private async Task SubmitAsync(object? value)
    {
        busy = true;

        try
        {
            if (await Submit(value))
            {
                MudDialog.Close(DialogResult.Ok(true));
            }
        }
        finally
        {
            busy = false;
        }
    }

    private void Cancel() => MudDialog.Cancel();
}
