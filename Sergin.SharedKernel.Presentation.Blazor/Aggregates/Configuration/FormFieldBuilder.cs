using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>Overrides for one field of a generated form, from <c>Form&lt;T&gt;(form =&gt; form.Field(x =&gt; x.Name))</c>.</summary>
public sealed class FormFieldBuilder
{
    private readonly FormFieldOverride field;

    internal FormFieldBuilder(FormFieldOverride field) => this.field = field;

    /// <summary>Replaces the label the field's <see cref="FieldText"/> declares; its hint and placeholder stay.</summary>
    public FormFieldBuilder Label(TextDefinition label)
    {
        ArgumentNullException.ThrowIfNull(label);

        field.Label = label;
        return this;
    }

    /// <summary>
    /// Edits the field with <typeparamref name="TComponent"/> in place of the generated input, which is how a field of a type
    /// no generated input edits (a collection) is shown. The component receives three parameters: <c>object? Value</c>,
    /// the form's current value; <c>EventCallback&lt;object?&gt; ValueChanged</c>, which it invokes with the new value, of
    /// the field's own type; and <c>FormFieldView Field</c>, its label and type. The pipeline's validator still checks
    /// the value on submit.
    /// </summary>
    public FormFieldBuilder Component<TComponent>()
        where TComponent : IComponent
    {
        field.Component = typeof(TComponent);
        return this;
    }
}

/// <summary>What a configuration declared for one form field.</summary>
internal sealed class FormFieldOverride(string name)
{
    public string Name { get; } = name;

    public TextDefinition? Label { get; set; }

    public Type? Component { get; set; }
}
