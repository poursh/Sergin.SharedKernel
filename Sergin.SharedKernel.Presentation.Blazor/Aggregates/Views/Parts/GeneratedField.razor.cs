using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// One <see cref="FieldView"/> of a loaded record as a <see cref="Pages.SerginDetailField"/>: the select or switch of an
/// inline set-property command when <see cref="InlineCommand"/> is given (the detail view gives it only to a user who
/// holds the command's permissions; a change is handed to <see cref="OnInlineChanged"/> at once), its configured component
/// (given <c>Context</c> and <c>Value</c>), a link when the field has an <c>Href</c>, else the value as text — a date in
/// the viewer's culture, anything else as the culture prints it. <see cref="FieldView.Get"/> answers a value object's
/// primitive already, so nothing here reads metadata.
/// </summary>
public sealed partial class GeneratedField
{
    [Parameter]
    [EditorRequired]
    public FieldView Field { get; set; } = default!;

    /// <summary>The loaded record, of the GetOne response's type.</summary>
    [Parameter]
    [EditorRequired]
    public object Record { get; set; } = default!;

    /// <summary>What a configured component receives besides the value.</summary>
    [Parameter]
    [EditorRequired]
    public AggregateViewContext Context { get; set; } = default!;

    /// <summary>The inline set-property command that edits this field as a choice, or null for the plain value.</summary>
    [Parameter]
    public CommandView? InlineCommand { get; set; }

    /// <summary>Runs with the chosen value; create it on the view so the view re-renders after it.</summary>
    [Parameter]
    public EventCallback<object?> OnInlineChanged { get; set; }

    /// <summary>True while the view sends the inline command; disables the input.</summary>
    [Parameter]
    public bool InlineBusy { get; set; }

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    private object? Value => Field.Get(Record);

    private string? Href => Field.Href?.Invoke(Record);

    private Dictionary<string, object?> ComponentParameters => new(StringComparer.Ordinal)
    {
        ["Context"] = Context,
        ["Value"] = Value,
    };

    // The field reader already read a value object through to its primitive; Format(...) applies here as in a list row.
    private string? Text => FieldValueText.Render(Field, Value, Formatter, Localizer);

    // The input names the field it edits (it has no visible label of its own: the field caption sits above it).
    private string LabelShown => Localizer.Text(Field.Label);

    // A choice's text: the command's value text, else the field's, else the member as it is.
    private string ChoiceShown(object? option)
    {
        if (option is null)
        {
            return string.Empty;
        }

        TextDefinition? text = InlineCommand?.ValueField?.ValueTexts?.GetValueOrDefault(option) ?? Field.ValueTexts?.GetValueOrDefault(option);

        return text is null ? option.ToString() ?? string.Empty : Localizer.Text(text);
    }

    private Task OnSelectChanged(object? value) => OnInlineChanged.InvokeAsync(value);

    private Task OnSwitchChanged(bool value) => OnInlineChanged.InvokeAsync(value);
}
