using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Presentation.Blazor.Pages;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

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
/// <param name="ConfirmTitle">The delete confirm's title, or null for the default.</param>
/// <param name="ConfirmText">The delete confirm's text, or null for the default.</param>
/// <param name="Hidden">Not offered as an action.</param>
/// <param name="MenuItems">Further entries behind the action's drop-down.</param>
/// <param name="Invoker">Sends the command without naming its response type.</param>
/// <param name="BuildFromId">Builds the command from the target's id; null unless its constructor is id-only (always set for a transition or delete).</param>
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
    Func<Guid, object>? BuildFromId);

/// <summary>An entry a configuration put behind a command's drop-down with <c>MenuItem(...)</c>.</summary>
public sealed record CommandMenuItemView(
    TextDefinition Label,
    string? Icon,
    Func<AggregateViewContext, Task> OnClick,
    PermissionDefinition? Permission);
