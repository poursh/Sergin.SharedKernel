using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Model;

/// <summary>
/// One command of a root as a generated view shows it: its role, look and policy. The policy comes from the command's
/// configuration and type (<c>CommandConfigurationRegistry</c>); the look from the view configuration or the role's
/// default. A <see cref="Hidden"/> command is not offered as an action; when it declared no label it carries the root's
/// singular name.
/// </summary>
/// <param name="Request">The command type.</param>
/// <param name="Role">What it does, from its effect interface.</param>
/// <param name="Label">The button text.</param>
/// <param name="Icon">A MudBlazor icon constant, or null.</param>
/// <param name="Kind">Its look and place in the actions bar.</param>
/// <param name="Permissions">The permissions its configuration requires.</param>
/// <param name="AnyScope">Whether they are checked on any scope (an aggregate that is not <c>Scoped()</c>).</param>
/// <param name="RequiresVersion">Whether it is sent at the record's version.</param>
/// <param name="ScheduleName">The name its configuration gave with <c>Schedulable(name)</c>, or null.</param>
/// <param name="Selectable">Whether it may run once per selected row (<c>ISelectable</c>).</param>
/// <param name="SucceededText">The text shown after it succeeds, placeholders filled from the loaded record; null for none.</param>
/// <param name="ConfirmTitle">The confirm's title (a delete's, or any command's from <c>Confirm(...)</c>), or null for the default.</param>
/// <param name="ConfirmText">
/// The confirm's text, or null: a delete then uses the default, and any other command is sent without asking.
/// </param>
/// <param name="Hidden">Not offered as an action.</param>
/// <param name="MenuItems">Further entries behind the action's drop-down.</param>
/// <param name="Invoker">Sends the command without naming its response type.</param>
/// <param name="BuildFromId">Builds the command from the target's id; null unless its constructor is id-only (always set for a transition or delete).</param>
/// <param name="BulkSucceededText">The text a list's bulk run reports (<c>{Count}</c>), or null for the default.</param>
/// <param name="ConfirmManyTitle">A list's bulk delete confirm's title, or null for the default.</param>
/// <param name="ConfirmManyText">A list's bulk delete confirm's text (<c>{Count}</c>), or null for the default.</param>
/// <param name="EditMode">Where an edit command is offered; <see cref="SerginEditMode.Dialog"/> unless declared with <c>Edit(...)</c>.</param>
/// <param name="DisabledWhen">Whether the action is disabled for a record or row (presentation only: the command stays callable and the pipeline still refuses); null for never.</param>
/// <param name="DisabledReason">The tooltip of a disabled action, or null.</param>
/// <param name="BuildFromIds">Builds a child command from the root's id and the child's id; null unless its constructor is exactly those two.</param>
/// <param name="ValueField">The one value a root's set-property command sets, as its value form renders it; null for any other command, and for a hidden one.</param>
/// <param name="BuildFromValues">
/// Builds a root's set-property command from the record's id and its value by field name (<paramref name="ValueField"/>'s),
/// or a delete from the record's id and the values its confirm asked for (<see cref="ConfirmFields"/>); null for any other command.
/// </param>
/// <param name="AppliesWhen">
/// Whether the command is offered for a record (one command of an <c>IReverseOf</c> pair, from <c>AppliesWhen</c>; its partner
/// carries the negation); a record that is none of the declared type, and a view with no record, offer neither. Null for always.
/// </param>
/// <param name="ConfirmLabel">The confirm button's text from <c>Confirm(...)</c>, or null for the command's label.</param>
/// <param name="BuildMove">
/// Builds a root's move command (<c>IMoveCommand</c>, constructor <c>(Id, NewParentId)</c>) from the record's id and the new
/// parent's id; null for any other command, and for a hidden one.
/// </param>
/// <param name="MoveParentField">The move command's <c>NewParentId</c> field text, which labels its dialog's parent picker; null with no <paramref name="BuildMove"/>.</param>
public sealed record CommandView(
    Type Request,
    CommandRole Role,
    TextDefinition Label,
    string? Icon,
    SerginActionKind Kind,
    IReadOnlyCollection<PermissionDefinition> Permissions,
    bool AnyScope,
    bool RequiresVersion,
    string? ScheduleName,
    bool Selectable,
    TextDefinition? SucceededText,
    TextDefinition? ConfirmTitle,
    TextDefinition? ConfirmText,
    bool Hidden,
    IReadOnlyList<CommandMenuItemView> MenuItems,
    RequestInvoker Invoker,
    Func<Guid, object>? BuildFromId,
    TextDefinition? BulkSucceededText = null,
    TextDefinition? ConfirmManyTitle = null,
    TextDefinition? ConfirmManyText = null,
    SerginEditMode EditMode = SerginEditMode.Dialog,
    Func<object, bool>? DisabledWhen = null,
    TextDefinition? DisabledReason = null,
    Func<Guid, Guid, object>? BuildFromIds = null,
    FormFieldView? ValueField = null,
    Func<Guid, IReadOnlyDictionary<string, object?>, object>? BuildFromValues = null,
    Func<object, bool>? AppliesWhen = null,
    TextDefinition? ConfirmLabel = null,
    Func<Guid, Guid, object>? BuildMove = null,
    FieldText? MoveParentField = null)
{
    /// <summary>
    /// The values a delete's confirm asks for: its constructor's shown parameters beside the <c>Id</c>, built with
    /// <see cref="BuildFromValues"/> (<see cref="BuildFromId"/> stays null). Empty for any other command and for an id-only
    /// delete. A list offers such a delete neither as a row action nor in bulk, having no dialog to ask with.
    /// </summary>
    public IReadOnlyList<FormFieldView> ConfirmFields { get; init; } = [];
}

/// <summary>An entry a configuration put behind a command's drop-down with <c>MenuItem(...)</c>.</summary>
public sealed record CommandMenuItemView(
    TextDefinition Label,
    string? Icon,
    Func<AggregateViewContext, Task> OnClick,
    PermissionDefinition? Permission);
