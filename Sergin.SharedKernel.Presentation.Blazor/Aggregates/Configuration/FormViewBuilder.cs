using System.Linq.Expressions;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class FormViewBuilder
{
    private protected FormViewBuilder(Type command) => Command = command;

    internal Type Command { get; }

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

    /// <summary>
    /// Renders the child-entity id <paramref name="property"/> as a picker of <typeparamref name="TParentRoot"/> (its
    /// list query) followed by a picker of its children (its <c>IChildListOf</c> query at the chosen parent).
    /// <paramref name="parentFromRecord"/> reads the parent's id from the loaded GetOne response, seeding an edit form.
    /// </summary>
    public FormViewBuilder<TCommand> ChildPicker<TParentRoot, TResponse>(
        Expression<Func<TCommand, object?>> property, Func<TResponse, Guid> parentFromRecord)
        where TParentRoot : class, IAggregateRoot
    {
        ArgumentNullException.ThrowIfNull(parentFromRecord);

        string name = ViewExpressions.PropertyOf(property, nameof(ChildPicker));
        ChildPickers[name] = new ChildPickerDeclaration(
            name, typeof(TParentRoot), typeof(TResponse), record => parentFromRecord((TResponse)record));
        return this;
    }
}

/// <summary>A <c>ChildPicker(...)</c> declaration.</summary>
internal sealed record ChildPickerDeclaration(string Name, Type ParentRoot, Type Response, Func<object, Guid> ParentFromRecord);
