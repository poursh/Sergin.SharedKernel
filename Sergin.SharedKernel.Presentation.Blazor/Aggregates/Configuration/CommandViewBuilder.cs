using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Domain.Securities;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class CommandViewBuilder
{
    private protected CommandViewBuilder(Type request) => Request = request;

    internal Type Request { get; }

    internal TextDefinition? DeclaredLabel { get; private protected set; }

    internal string? DeclaredIcon { get; private protected set; }

    internal TextDefinition? DeclaredSucceededText { get; private protected set; }

    internal TextDefinition? ConfirmTitle { get; private protected set; }

    internal TextDefinition? ConfirmText { get; private protected set; }

    internal TextDefinition? DeclaredBulkSucceededText { get; private protected set; }

    internal TextDefinition? ConfirmManyTitle { get; private protected set; }

    internal TextDefinition? ConfirmManyText { get; private protected set; }

    internal bool IsHidden { get; private protected set; }

    internal SerginEditMode DeclaredEditMode { get; private protected set; } = SerginEditMode.Dialog;

    /// <summary>The record type <c>DisabledWhen&lt;TRecord&gt;</c> reads, or null.</summary>
    internal Type? DisabledRecord { get; private protected set; }

    internal Func<object, bool>? DeclaredDisabledWhen { get; private protected set; }

    internal TextDefinition? DeclaredDisabledReason { get; private protected set; }

    internal List<CommandMenuItemView> MenuItems { get; } = [];
}

/// <summary>How one command of the root is offered: what the command's type cannot imply.</summary>
public sealed class CommandViewBuilder<TCommand> : CommandViewBuilder
    where TCommand : IBaseCommand
{
    internal CommandViewBuilder()
        : base(typeof(TCommand))
    {
    }

    /// <summary>The button text. Required for a transition, set-property or move command, which have no default.</summary>
    public CommandViewBuilder<TCommand> Label(TextDefinition label)
    {
        ArgumentNullException.ThrowIfNull(label);
        DeclaredLabel = label;
        return this;
    }

    /// <summary>A MudBlazor icon constant, replacing the role's default.</summary>
    public CommandViewBuilder<TCommand> Icon(string icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        DeclaredIcon = icon;
        return this;
    }

    /// <summary>The text shown after the command succeeds; its placeholders are filled from the loaded record's properties.</summary>
    public CommandViewBuilder<TCommand> SucceededText(TextDefinition text)
    {
        ArgumentNullException.ThrowIfNull(text);
        DeclaredSucceededText = text;
        return this;
    }

    /// <summary>The delete confirm's title and text, replacing the defaults.</summary>
    public CommandViewBuilder<TCommand> ConfirmDelete(TextDefinition title, TextDefinition text)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(text);
        ConfirmTitle = title;
        ConfirmText = text;
        return this;
    }

    /// <summary>
    /// The text a list's bulk run of the command reports, replacing the default; <c>{Count}</c> is the number of rows it
    /// ran on.
    /// </summary>
    public CommandViewBuilder<TCommand> BulkSucceededText(TextDefinition text)
    {
        ArgumentNullException.ThrowIfNull(text);
        DeclaredBulkSucceededText = text;
        return this;
    }

    /// <summary>A list's bulk delete confirm's title and text, replacing the defaults; <c>{Count}</c> is the number of selected rows.</summary>
    public CommandViewBuilder<TCommand> ConfirmDeleteMany(TextDefinition title, TextDefinition text)
    {
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(text);
        ConfirmManyTitle = title;
        ConfirmManyText = text;
        return this;
    }

    /// <summary>Where an edit command is offered; <see cref="SerginEditMode.Dialog"/> by default.</summary>
    public CommandViewBuilder<TCommand> Edit(SerginEditMode mode)
    {
        DeclaredEditMode = mode;
        return this;
    }

    /// <summary>
    /// Disables the action for a record or row of <typeparamref name="TRecord"/> that satisfies <paramref name="when"/>,
    /// with <paramref name="reason"/> as its tooltip. Presentation only: the command stays callable and the pipeline still
    /// refuses. <typeparamref name="TRecord"/> must be a record the command's views hold: the list item or GetOne response
    /// of the root or of the child.
    /// </summary>
    public CommandViewBuilder<TCommand> DisabledWhen<TRecord>(Func<TRecord, bool> when, TextDefinition? reason = null)
    {
        ArgumentNullException.ThrowIfNull(when);

        DisabledRecord = typeof(TRecord);
        DeclaredDisabledWhen = record => record is TRecord typed && when(typed);
        DeclaredDisabledReason = reason;
        return this;
    }

    /// <summary>Not offered as an action (a command a custom component or another page sends).</summary>
    public CommandViewBuilder<TCommand> Hidden()
    {
        IsHidden = true;
        return this;
    }

    /// <summary>An entry behind the action's drop-down (a split button), hidden from a user without <paramref name="permission"/>.</summary>
    public CommandViewBuilder<TCommand> MenuItem(
        TextDefinition label, string? icon, Func<AggregateViewContext, Task> onClick, PermissionDefinition? permission = null)
    {
        ArgumentNullException.ThrowIfNull(label);
        ArgumentNullException.ThrowIfNull(onClick);
        MenuItems.Add(new CommandMenuItemView(label, icon, onClick, permission));
        return this;
    }
}
