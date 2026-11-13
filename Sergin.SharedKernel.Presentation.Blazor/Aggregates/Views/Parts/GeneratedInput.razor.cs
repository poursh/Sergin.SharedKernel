using System.Globalization;
using Microsoft.AspNetCore.Components;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Errors;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// One <see cref="FormFieldView"/> of a generated form, bound to its <see cref="GeneratedFormValues"/>: a text, number,
/// switch, date or select input for the primitive the field wraps; a picker over another root's list (its first
/// <see cref="PickerOptions.MaxOptions"/> records); or, for a child entity, a parent picker then the chosen parent's
/// children, disabled until a parent is chosen. Every input validates its field through <see cref="Validate"/>.
/// </summary>
public sealed partial class GeneratedInput
{
    private IReadOnlyList<PickerOption> options = [];
    private IReadOnlyList<PickerOption> parentOptions = [];
    private bool optionsLoaded;
    private Guid? optionsParent;

    private Func<string?, Task<IEnumerable<string>>> textValidation = default!;
    private Func<decimal?, Task<IEnumerable<string>>> numberValidation = default!;
    private Func<bool, Task<IEnumerable<string>>> switchValidation = default!;
    private Func<DateTime?, Task<IEnumerable<string>>> dateValidation = default!;
    private Func<object?, Task<IEnumerable<string>>> selectValidation = default!;
    private Func<Guid, Task<IEnumerable<string>>> guidValidation = default!;

    [Parameter]
    [EditorRequired]
    public FormFieldView Field { get; set; } = default!;

    [Parameter]
    [EditorRequired]
    public GeneratedFormValues Values { get; set; } = default!;

    /// <summary>The pipeline validator's messages for a field, by its name, over the form's current values.</summary>
    [Parameter]
    [EditorRequired]
    public Func<string, Task<IEnumerable<string>>> Validate { get; set; } = default!;

    [Inject]
    private ISerginDispatcher Dispatcher { get; set; } = default!;

    [Inject]
    private IUiErrorPresenter ErrorPresenter { get; set; } = default!;

    [Inject]
    private IStringLocalizer Localizer { get; set; } = default!;

    [Inject]
    private ICultureFormatter Formatter { get; set; } = default!;

    private string LabelShown => Localizer.Text(Field.Label.Label);

    private string? PlaceholderShown => Field.Label.Placeholder is { } placeholder ? Localizer.Text(placeholder) : null;

    private string? ParentLabelShown => Field.ParentLabel is { } parent ? Localizer.Text(parent.Label) : null;

    private string? ParentPlaceholderShown => Field.ParentLabel?.Placeholder is { } placeholder ? Localizer.Text(placeholder) : null;

    // Until a parent is picked the child picker is disabled and says why.
    private string? ChildPlaceholderShown =>
        ParentSelection == Guid.Empty && Field.ParentFirst is { } first
            ? Localizer.Text(first, new { Parent = ParentLabelShown })
            : PlaceholderShown;

    // View state only: the parent narrows the child picker and is not part of the command.
    private Guid ParentSelection => Values.ParentOf(Field.Name);

    private Guid GuidValue => Values.ValueOf(Field.Name) is Guid value ? value : Guid.Empty;

    private decimal? NumberValue => Values.ValueOf(Field.Name) is { } value ? Convert.ToDecimal(value, CultureInfo.InvariantCulture) : null;

    private bool SwitchValue => Values.ValueOf(Field.Name) is true;

    private DateTime? DateValue => Values.ValueOf(Field.Name) switch
    {
        DateOnly date => date.ToDateTime(TimeOnly.MinValue),
        DateTimeOffset instant => instant.DateTime,
        DateTime date => date,
        _ => null,
    };

    protected override void OnInitialized()
    {
        textValidation = value => StoreAndValidateAsync(value);
        numberValidation = value => StoreAndValidateAsync(value);
        switchValidation = value => StoreAndValidateAsync(value);
        dateValidation = value => StoreAndValidateAsync(DateOf(value));
        selectValidation = value => StoreAndValidateAsync(value);
        guidValidation = value => StoreAndValidateAsync(value);
    }

    // The parent's options once; the child's whenever the parent was set from outside (an edit form's load or reload).
    protected override async Task OnParametersSetAsync()
    {
        switch (Field.Kind)
        {
            case FormInputKind.Picker when !optionsLoaded:
                optionsLoaded = true;
                options = await LoadAsync(Field.Options!, Guid.Empty);
                break;
            case FormInputKind.ChildPicker:
                if (!optionsLoaded)
                {
                    optionsLoaded = true;
                    parentOptions = await LoadAsync(Field.ParentOptions!, Guid.Empty);
                }

                if (optionsParent != ParentSelection)
                {
                    await LoadChildrenAsync(ParentSelection);
                }

                break;
        }
    }

    // The input's own value is what it validates: stored first, so the command built for the rule carries it.
    private Task<IEnumerable<string>> StoreAndValidateAsync(object? value)
    {
        Values.Set(Field.Name, value);

        return Validate(Field.Name);
    }

    private void OnTextChanged(string? value) => Values.Set(Field.Name, value);

    private void OnNumberChanged(decimal? value) => Values.Set(Field.Name, value);

    private void OnSwitchChanged(bool value) => Values.Set(Field.Name, value);

    private void OnDateChanged(DateTime? value) => Values.Set(Field.Name, DateOf(value));

    private void OnSelectChanged(object? value) => Values.Set(Field.Name, value);

    private void OnGuidChanged(Guid value) => Values.Set(Field.Name, value);

    // A new parent empties the child's value and lists the parent's children.
    private async Task OnParentChangedAsync(Guid parent)
    {
        Values.SetParent(Field.Name, parent);
        Values.Set(Field.Name, Guid.Empty);

        await LoadChildrenAsync(parent);
    }

    private async Task LoadChildrenAsync(Guid parent)
    {
        optionsParent = parent;
        options = [];

        if (parent == Guid.Empty)
        {
            return;
        }

        options = await LoadAsync(Field.Options!, parent);
    }

    private async Task<IReadOnlyList<PickerOption>> LoadAsync(PickerOptions source, Guid parent)
    {
        ErrorOr<IReadOnlyList<PickerOption>> loaded = await source.LoadAsync(Dispatcher, parent);

        if (loaded.IsError)
        {
            ErrorPresenter.Notify(loaded.FirstError);

            return [];
        }

        return loaded.Value;
    }

    // The picked day as the primitive the field wraps.
    private object? DateOf(DateTime? value) => value switch
    {
        null => null,
        { } day when Field.Primitive == typeof(DateOnly) => DateOnly.FromDateTime(day),
        { } day when Field.Primitive == typeof(DateTimeOffset) => new DateTimeOffset(day),
        { } day => day,
    };
}
