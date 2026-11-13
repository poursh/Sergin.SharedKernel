using Microsoft.AspNetCore.Components;
using MudBlazor;
using Sergin.SharedKernel.Presentation.Blazor.Validation;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// The value form of a root's set-property command (<see cref="CommandView.ValueField"/>): one generated input, checked
/// field by field by the pipeline's own validator over the command its value builds (<see cref="GeneratedSetProperty"/>),
/// and a submit that validates first and hands the value to <see cref="OnSubmit"/> only when it passes.
/// </summary>
/// <remarks>
/// The form is seeded from <see cref="Value"/>, and seeded again only when <see cref="Value"/> itself changes (a reload
/// that found another value), so a refused submit keeps what the user typed while the record stays as it was.
/// </remarks>
public sealed partial class GeneratedValueForm
{
    private readonly GeneratedFormValues values = new();

    private MudForm form = default!;
    private bool isValid = true;
    private bool seeded;
    private object? seededFrom;
    private GeneratedSetProperty setProperty = default!;
    private Func<object, string, Task<IEnumerable<string>>> validation = default!;

    /// <summary>A root's shown set-property command: one with a <see cref="CommandView.ValueField"/>.</summary>
    [Parameter]
    [EditorRequired]
    public CommandView Command { get; set; } = default!;

    /// <summary>The record's id, which the command is built with.</summary>
    [Parameter]
    public Guid Id { get; set; }

    /// <summary>The value the form starts from: the record's property of the value's name, or null.</summary>
    [Parameter]
    public object? Value { get; set; }

    /// <summary>Runs with the value once it passes validation; create it on the view so the view re-renders after it.</summary>
    [Parameter]
    public EventCallback<object?> OnSubmit { get; set; }

    /// <summary>True while the view sends; disables the submit.</summary>
    [Parameter]
    public bool Busy { get; set; }

    /// <summary>The submit's text; the command's label when not set.</summary>
    [Parameter]
    public TextDefinition? SubmitLabel { get; set; }

    /// <summary>Further buttons after the submit (a dialog's Cancel).</summary>
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Inject]
    private ISerginFormValidator FormValidator { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    // An EventCallback, not a bare delegate: splatted onto MudForm's <form> it gets a receiver, so the form re-renders.
    private EventCallback OnFormSubmit => EventCallback.Factory.Create(this, SubmitAsync);

    private string ValueName => Command.ValueField!.Name;

    protected override void OnInitialized()
    {
        setProperty = new GeneratedSetProperty(FormValidator);

        // MudForm's form-level rule, for an input with a For (a child picker's parent select): the field by its path.
        validation = async (_, propertyName) => await ValidateAsync(propertyName);
    }

    protected override void OnParametersSet()
    {
        if (!seeded || !Equals(Value, seededFrom))
        {
            values.Set(ValueName, Value);
            seededFrom = Value;
            seeded = true;
        }
    }

    private async Task<IEnumerable<string>> ValidateAsync(string propertyName) =>
        await setProperty.ValidateAsync(Command, Id, values.ValueOf(ValueName), propertyName);

    private async Task SubmitAsync()
    {
        await form.ValidateAsync();

        if (!form.IsValid)
        {
            return;
        }

        await OnSubmit.InvokeAsync(values.ValueOf(ValueName));
    }
}
