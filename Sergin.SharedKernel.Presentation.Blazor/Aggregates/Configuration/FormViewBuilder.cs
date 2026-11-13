using System.Linq.Expressions;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class FormViewBuilder
{
    private protected FormViewBuilder(Type command) => Command = command;

    internal Type Command { get; }

    /// <summary>The form's title, from <c>Title(...)</c>; null for the shell's "New {Name}" / "Edit {Name}".</summary>
    internal TextDefinition? DeclaredTitle { get; private protected set; }

    /// <summary>The child pickers declared, by field name.</summary>
    internal Dictionary<string, ChildPickerDeclaration> ChildPickers { get; } = new(StringComparer.Ordinal);
}

/// <summary>Overrides for the generated form of the root's create or update command.</summary>
public sealed class FormViewBuilder<TCommand> : FormViewBuilder
    where TCommand : IBaseCommand
{
    internal FormViewBuilder()
        : base(typeof(TCommand))
    {
    }

    /// <summary>The form's page title, heading and last breadcrumb step, in place of the shell's "New {Name}" / "Edit {Name}".</summary>
    public FormViewBuilder<TCommand> Title(TextDefinition title)
    {
        ArgumentNullException.ThrowIfNull(title);

        DeclaredTitle = title;
        return this;
    }

    /// <summary>
    /// Renders the child-entity id <paramref name="property"/> as a picker of <typeparamref name="TParentRoot"/> (its
    /// list query) followed by a picker of its children (its <c>IChildListOf</c> query at the chosen parent).
    /// <paramref name="parentFromRecord"/> reads the parent's id from the loaded GetOne response, seeding an edit form.
    /// <paramref name="parentFirst"/> is the child picker's placeholder while no parent is chosen; null for the shell's
    /// "Select {Parent} first".
    /// </summary>
    public FormViewBuilder<TCommand> ChildPicker<TParentRoot, TResponse>(
        Expression<Func<TCommand, object?>> property, Func<TResponse, Guid> parentFromRecord, TextDefinition? parentFirst = null)
        where TParentRoot : class, IAggregateRoot
    {
        ArgumentNullException.ThrowIfNull(parentFromRecord);

        string name = ViewExpressions.PropertyOf(property, nameof(ChildPicker));
        ChildPickers[name] = new ChildPickerDeclaration(
            name, typeof(TParentRoot), typeof(TResponse), record => parentFromRecord((TResponse)record), parentFirst);
        return this;
    }
}

/// <summary>A <c>ChildPicker(...)</c> declaration.</summary>
internal sealed record ChildPickerDeclaration(
    string Name, Type ParentRoot, Type Response, Func<object, Guid> ParentFromRecord, TextDefinition? ParentFirst);
