using System.Linq.Expressions;
using System.Reflection;
using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>The non-generic half, so the registry can read what any closed builder collected.</summary>
public abstract class FieldSetBuilder
{
    private protected FieldSetBuilder(Type record) => Record = record;

    internal Type Record { get; }

    /// <summary>The fields the configuration named, by property name, in the order it named them.</summary>
    internal Dictionary<string, FieldOverride> Overrides { get; } = new(StringComparer.Ordinal);

    /// <summary>The display property named with <c>Title(...)</c>, or null for the default.</summary>
    internal string? TitleProperty { get; private protected set; }
}

/// <summary>Overrides for the fields of a GetOne response or list item.</summary>
public class FieldSetBuilder<T> : FieldSetBuilder
{
    internal FieldSetBuilder()
        : base(typeof(T))
    {
    }

    /// <summary>The field of <paramref name="property"/>, which must name a property of the record itself (<c>x =&gt; x.Name</c>).</summary>
    public FieldBuilder<T> Field(Expression<Func<T, object?>> property)
    {
        string name = ViewExpressions.PropertyOf(property, nameof(Field));

        if (!Overrides.TryGetValue(name, out FieldOverride? field))
        {
            field = new FieldOverride(name);
            Overrides.Add(name, field);
        }

        return new FieldBuilder<T>(field);
    }

    /// <summary>
    /// The record's display field: the breadcrumb tail, the fields title and a schedule's target label. By default the
    /// first <see cref="string"/> property with a <see cref="FieldText"/>.
    /// </summary>
    public FieldSetBuilder<T> Title(Expression<Func<T, object?>> property)
    {
        TitleProperty = ViewExpressions.PropertyOf(property, nameof(Title));
        return this;
    }
}

/// <summary>Overrides for one field.</summary>
public sealed class FieldBuilder<T>
{
    private readonly FieldOverride field;

    internal FieldBuilder(FieldOverride field) => this.field = field;

    /// <summary>Renders the value as a link to <paramref name="href"/> of the record.</summary>
    public FieldBuilder<T> Link(Func<T, string> href)
    {
        ArgumentNullException.ThrowIfNull(href);
        field.Href = record => href((T)record);
        return this;
    }

    /// <summary>Not shown.</summary>
    public FieldBuilder<T> Hidden()
    {
        field.Hidden = true;
        return this;
    }

    /// <summary>Its place among the fields, replacing the declaration order.</summary>
    public FieldBuilder<T> Order(int order)
    {
        field.Order = order;
        return this;
    }

    /// <summary>Renders the value with <typeparamref name="TComponent"/>, which receives <c>[Parameter] AggregateViewContext Context</c> and <c>[Parameter] object? Value</c>.</summary>
    public FieldBuilder<T> Component<TComponent>()
        where TComponent : IComponent
    {
        field.Component = typeof(TComponent);
        return this;
    }
}

/// <summary>What a configuration declared for one field; named, so it is shown unless <see cref="Hidden"/>.</summary>
internal sealed class FieldOverride(string name)
{
    public string Name { get; } = name;

    public Func<object, string>? Href { get; set; }

    public bool Hidden { get; set; }

    public int? Order { get; set; }

    public Type? Component { get; set; }
}

internal static class ViewExpressions
{
    /// <summary>The property <paramref name="property"/> names on its parameter's type, or a refusal naming the expression.</summary>
    public static string PropertyOf<T, TValue>(Expression<Func<T, TValue>> property, string method)
    {
        ArgumentNullException.ThrowIfNull(property);

        Expression body = property.Body is UnaryExpression { NodeType: ExpressionType.Convert } conversion
            ? conversion.Operand
            : property.Body;

        if (body is not MemberExpression { Expression: ParameterExpression, Member: PropertyInfo member }
            || !member.DeclaringType!.IsAssignableFrom(typeof(T)))
        {
            throw new InvalidOperationException(
                $"{method}(...) on {typeof(T).FullName} must name a property of {typeof(T).Name} itself, e.g. x => x.Name; "
                + $"'{property}' is not a property of {typeof(T).FullName}.");
        }

        return member.Name;
    }
}
