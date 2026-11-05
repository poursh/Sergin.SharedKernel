using System.Linq.Expressions;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class CommandConfigurationBuilder
{
    internal List<PermissionDefinition> Permissions { get; } = [];

    internal bool ExpectedVersionRequired { get; private protected set; }

    /// <summary>The text category the request's fields are declared in, or null to take its bound root's.</summary>
    internal TextCategory? TextCategory { get; private protected set; }

    /// <summary>The request properties no person sees, so they need no field text.</summary>
    internal HashSet<string> HiddenProperties { get; } = new(StringComparer.Ordinal);
}

public sealed class CommandConfigurationBuilder<TCommand> : CommandConfigurationBuilder
    where TCommand : IBaseCommand
{
    internal CommandConfigurationBuilder()
    {
    }

    /// <summary>
    /// The caller must hold every listed permission, each a definition an aggregate registered with
    /// <c>Permissions(...)</c> (or <see cref="PermissionDefinition.AllPlatform"/>): the registry refuses any other.
    /// Calling it again appends; a repeated permission counts once.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> RequirePermissions(PermissionDefinition permission, params PermissionDefinition[] more)
    {
        ArgumentNullException.ThrowIfNull(permission);
        ArgumentNullException.ThrowIfNull(more);

        Permissions.Add(permission);
        Permissions.AddRange(more);
        return this;
    }

    /// <summary>
    /// A send without an expected version answers <c>VersionErrors.Required</c> (428) before the handler runs.
    /// Commands only: the registry refuses it on a query, and on a command bound to an aggregate that is not
    /// <c>Versioned()</c>.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> RequireExpectedVersion()
    {
        ExpectedVersionRequired = true;
        return this;
    }

    /// <summary>
    /// Names the text category the request's <see cref="FieldText"/>s are declared in, for the field labels in its
    /// validation messages. A request bound to an aggregate takes its root's category without this; calling it
    /// overrides that. Calling it again replaces the category.
    /// </summary>
    public CommandConfigurationBuilder<TCommand> Texts(TextCategory category)
    {
        ArgumentNullException.ThrowIfNull(category);

        TextCategory = category;
        return this;
    }

    /// <summary>
    /// Marks a property of the request as never shown to a person (an id, a paging record), so it needs no
    /// <see cref="FieldText"/> in the request's category. <paramref name="property"/> must name a property of the
    /// request itself (<c>command =&gt; command.Id</c>).
    /// </summary>
    public CommandConfigurationBuilder<TCommand> NotShown<TProperty>(Expression<Func<TCommand, TProperty>> property)
    {
        ArgumentNullException.ThrowIfNull(property);

        Expression body = property.Body is UnaryExpression { NodeType: ExpressionType.Convert } conversion
            ? conversion.Operand
            : property.Body;

        if (body is not MemberExpression { Expression: ParameterExpression } member)
        {
            throw new ArgumentException(
                $"NotShown on {typeof(TCommand).FullName} must name a property of the request itself, e.g. command => command.Id; "
                + $"'{property}' does not.",
                nameof(property));
        }

        HiddenProperties.Add(member.Member.Name);
        return this;
    }
}
