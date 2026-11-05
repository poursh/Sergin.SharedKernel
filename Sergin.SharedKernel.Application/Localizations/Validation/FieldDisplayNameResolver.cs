using System.Linq.Expressions;
using System.Reflection;
using Microsoft.Extensions.Localization;
using Sergin.SharedKernel.Application.Commands.Configuration;

namespace Sergin.SharedKernel.Application.Localizations.Validation;

/// <summary>
/// The name a validation message shows for a property (spec decision 14): the label of the <see cref="FieldText"/> of
/// that property in the request's text category, which its configuration names (or its bound root's), in the current
/// UI culture. Null when the request has no category or the category no such field, so FluentValidation falls back to
/// splitting the property name, as before.
/// </summary>
public sealed class FieldDisplayNameResolver(CommandConfigurationRegistry registry, TextCatalog catalog, IStringLocalizer localizer)
{
    /// <summary>
    /// The label for the rule on <paramref name="member"/> of <paramref name="type"/>. A rule over a value object's
    /// inner value (<c>x =&gt; x.UserName.Value</c>, renamed with <c>OverridePropertyName</c>) is named after the
    /// request property it starts from, which is the name it is reported under.
    /// </summary>
    public string? Resolve(Type type, MemberInfo? member, LambdaExpression? expression)
    {
        ArgumentNullException.ThrowIfNull(type);

        string? propertyName = RequestPropertyOf(expression) ?? member?.Name;

        if (propertyName is null || registry.CategoryOf(type) is not { } category)
        {
            return null;
        }

        return catalog.FindField(category, propertyName) is { } field ? localizer.Text(field.Label) : null;
    }

    /// <summary>The first member read off the lambda's parameter: <c>UserName</c> in <c>x =&gt; x.UserName.Value</c>.</summary>
    private static string? RequestPropertyOf(LambdaExpression? expression)
    {
        Expression? current = expression?.Body;

        while (current is UnaryExpression { NodeType: ExpressionType.Convert } conversion)
        {
            current = conversion.Operand;
        }

        MemberExpression? first = null;

        while (current is MemberExpression member)
        {
            first = member;
            current = member.Expression;
        }

        return current is ParameterExpression ? first?.Member.Name : null;
    }
}
