namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>The input a generated form renders for a command field, from the primitive the field's type wraps.</summary>
public enum FormInputKind
{
    Text,
    Number,
    Switch,
    Date,
    Select,

    /// <summary>A typed id of another root: options from that root's list query.</summary>
    Picker,

    /// <summary>A typed id of a child entity: a parent picker, then the child list of the chosen parent.</summary>
    ChildPicker,
}

/// <summary>
/// One shown constructor parameter of a create or update command, as its generated form renders it: the command's
/// primary-constructor parameters minus the ones its type declares (<c>Id</c>, <c>RootId</c>, …) and the ones its
/// configuration marks <c>NotShown(...)</c>.
/// </summary>
/// <param name="Name">The parameter (and property) name.</param>
/// <param name="Label">Its declared texts.</param>
/// <param name="ValueType">The constructor parameter's type.</param>
/// <param name="Kind">The input rendered.</param>
/// <param name="PickerRoot">For a <see cref="FormInputKind.Picker"/>, the root offered; for a <see cref="FormInputKind.ChildPicker"/>, the child entity type.</param>
/// <param name="PickerParentRoot">For a <see cref="FormInputKind.ChildPicker"/>, the child's root, picked first.</param>
/// <param name="ParentFromRecord">For a <see cref="FormInputKind.ChildPicker"/>, the parent's id read from the loaded GetOne response, seeding an edit form.</param>
public sealed record FormFieldView(
    string Name,
    FieldText Label,
    Type ValueType,
    FormInputKind Kind,
    Type? PickerRoot,
    Type? PickerParentRoot,
    Func<object, Guid>? ParentFromRecord);
