using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views;

/// <summary>
/// The confirm of a delete that takes values beside the id (<see cref="CommandView.ConfirmFields"/>): the confirm text, a
/// generated input per value, Cancel and the confirm button. Confirm closes it with the values by field name (an
/// <see cref="IReadOnlyDictionary{TKey, TValue}"/> of <see cref="string"/> to <see cref="object"/>, the primitives the inputs
/// edit; an untouched value is absent and the request factory takes its default); Cancel closes it cancelled.
/// </summary>
/// <remarks>
/// The inputs run no field validation: the values are what a delete reads (a cascade switch), and the pipeline's validator
/// still checks the command when the view sends it.
/// </remarks>
public sealed partial class SerginConfirmValuesDialog
{
    private readonly GeneratedFormValues values = new();

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    /// <summary>The confirm text, already localized with the record's placeholders filled.</summary>
    [Parameter]
    [EditorRequired]
    public string Text { get; set; } = default!;

    /// <summary>The values to ask for.</summary>
    [Parameter]
    [EditorRequired]
    public IReadOnlyList<FormFieldView> Fields { get; set; } = [];

    /// <summary>The confirm button's text.</summary>
    [Parameter]
    [EditorRequired]
    public TextDefinition ConfirmLabel { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private static Func<string, Task<IEnumerable<string>>> NoMessages { get; } = _ => Task.FromResult(Enumerable.Empty<string>());

    // Internal, not private: the delete-with-values test drives Confirm and Cancel through these, as the buttons do.
    internal void Confirm()
    {
        IReadOnlyDictionary<string, object?> answered = new Dictionary<string, object?>(values.Values, StringComparer.Ordinal);
        MudDialog.Close(DialogResult.Ok(answered));
    }

    internal void Cancel() => MudDialog.Cancel();
}
