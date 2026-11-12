using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// The primitive a command field's type carries: a primitive, <see cref="string"/>, an enum or a date/time/Guid type
/// is itself; a record wrapping one value (one public constructor parameter, <c>DeviceId(string Value)</c>) is that
/// value's primitive; anything else has none. Nullable types are read through. <see cref="Wrap"/> and
/// <see cref="Unwrap"/> convert between a primitive and its wrapper, the way a form value reaches a command.
/// </summary>
public static class ValueObjects
{
    private const int MaxDepth = 4;

    private static readonly HashSet<Type> knownPrimitives =
    [
        typeof(string), typeof(decimal), typeof(Guid), typeof(DateTime), typeof(DateOnly), typeof(DateTimeOffset),
        typeof(TimeOnly), typeof(TimeSpan),
    ];

    private static readonly HashSet<Type> numbers =
    [
        typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint), typeof(long),
        typeof(ulong), typeof(float), typeof(double), typeof(decimal),
    ];

    private static readonly HashSet<Type> dates = [typeof(DateTime), typeof(DateOnly), typeof(DateTimeOffset)];

    public static Type? PrimitiveOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        Type current = type;

        for (int depth = 0; depth < MaxDepth; depth++)
        {
            current = Nullable.GetUnderlyingType(current) ?? current;

            if (IsPrimitive(current))
            {
                return current;
            }

            if (WrappedValueOf(current) is not { } wrapped)
            {
                return null;
            }

            current = wrapped;
        }

        return null;
    }

    /// <summary>
    /// <paramref name="primitive"/> as <paramref name="type"/> (see <see cref="Converter"/>). Reflects on every call: for
    /// build-time and test use, never the send path.
    /// </summary>
    public static object? Wrap(Type type, object? primitive) => Converter(type)(primitive);

    /// <summary>
    /// A converter from a primitive to <paramref name="type"/>, made once and then free of reflection: a wrapper is
    /// constructed around the (recursively converted) primitive through a compiled constructor; a value already of the
    /// type, or null, is returned as it is; a primitive of another kind is converted. With <paramref name="neverNull"/>
    /// a null becomes the primitive's default (empty string, <see cref="Guid.Empty"/>, 0, false) before conversion, so a
    /// wrapper is never null and a validator reading its <c>Value</c> cannot fail.
    /// </summary>
    public static Func<object?, object?> Converter(Type type, bool neverNull = false)
    {
        ArgumentNullException.ThrowIfNull(type);

        Type target = Nullable.GetUnderlyingType(type) ?? type;
        Func<object?, object?> core = CoreConverter(target);

        if (!neverNull || PrimitiveOf(target) is not { } primitive)
        {
            return core;
        }

        object fallback = primitive == typeof(string) ? string.Empty : Activator.CreateInstance(primitive)!;

        return value => core(value ?? fallback);
    }

    private static Func<object?, object?> CoreConverter(Type target)
    {
        if (target.IsEnum)
        {
            return value => value switch
            {
                null => null,
                string name => Enum.Parse(target, name),
                _ => target.IsInstanceOfType(value) ? value : Enum.ToObject(target, value),
            };
        }

        if (target == typeof(Guid))
        {
            return value => value is string text ? Guid.Parse(text) : value;
        }

        if (IsPrimitive(target))
        {
            return value => value is null || target.IsInstanceOfType(value) ? value : Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        }

        if (WrappedValueOf(target) is { } inner)
        {
            ConstructorInfo constructor = target.GetConstructors()
                .Single(candidate => candidate.GetParameters() is [{ } only] && only.ParameterType == inner);
            ParameterExpression argument = Expression.Parameter(typeof(object), "value");
            Func<object, object> construct = Expression
                .Lambda<Func<object, object>>(
                    Expression.Convert(Expression.New(constructor, Expression.Convert(argument, inner)), typeof(object)), argument)
                .Compile();
            Func<object?, object?> innerConverter = Converter(inner);

            return value => value is null || target.IsInstanceOfType(value) ? value : construct(innerConverter(value)!);
        }

        throw new InvalidOperationException($"{target.FullName} is neither a primitive nor a single-value wrapper, so a view cannot build one from a form value.");
    }

    /// <summary>The primitive inside <paramref name="value"/>, through any number of wrappers; a primitive or null is itself.</summary>
    public static object? Unwrap(object? value)
    {
        object? current = value;

        for (int depth = 0; current is not null && depth < MaxDepth; depth++)
        {
            Type type = current.GetType();

            if (IsPrimitive(type) || WrappedValueOf(type) is null)
            {
                return current;
            }

            current = InnerPropertyOf(type).GetValue(current);
        }

        return current;
    }

    /// <summary>
    /// A compiled reader of <paramref name="property"/> that answers the primitive inside its value, through any number
    /// of wrappers (null-safe), as <c>object</c>: made once at registry build, so a render reads no metadata.
    /// </summary>
    public static Func<object, object?> UnwrappedGetter(PropertyInfo property)
    {
        ArgumentNullException.ThrowIfNull(property);

        ParameterExpression record = Expression.Parameter(typeof(object), "record");
        Expression value = Expression.Property(Expression.Convert(record, property.DeclaringType!), property);

        return Expression.Lambda<Func<object, object?>>(Unwrapped(value), record).Compile();
    }

    // The value read through each wrapper's inner property; a null wrapper yields null.
    private static Expression Unwrapped(Expression value)
    {
        Expression current = Expression.Convert(value, typeof(object));
        Type type = value.Type;

        for (int depth = 0; depth < MaxDepth && !IsPrimitive(Nullable.GetUnderlyingType(type) ?? type) && WrappedValueOf(type) is not null; depth++)
        {
            PropertyInfo inner = InnerPropertyOf(type);
            ParameterExpression wrapper = Expression.Variable(type, "wrapper");

            current = Expression.Block(
                [wrapper],
                Expression.Assign(wrapper, Expression.Convert(current, type)),
                Expression.Condition(
                    Expression.Equal(wrapper, Expression.Constant(null, type)),
                    Expression.Constant(null, typeof(object)),
                    Expression.Convert(Expression.Property(wrapper, inner), typeof(object))));
            type = inner.PropertyType;
        }

        return current;
    }

    private static PropertyInfo InnerPropertyOf(Type type)
    {
        ParameterInfo parameter = type.GetConstructors()
            .Select(constructor => constructor.GetParameters())
            .Single(parameters => parameters.Length == 1 && parameters[0].ParameterType != type)[0];

        return type.GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
            ?? throw new InvalidOperationException($"{type.FullName} has no property for its constructor value '{parameter.Name}'.");
    }

    public static bool IsNumber(Type primitive) => numbers.Contains(primitive);

    public static bool IsDate(Type primitive) => dates.Contains(primitive);

    private static bool IsPrimitive(Type type) => type.IsPrimitive || type.IsEnum || knownPrimitives.Contains(type);

    /// <summary>A record's single constructor value, its copy constructor aside; null for anything else.</summary>
    private static Type? WrappedValueOf(Type type)
    {
        if (!type.IsClass || type.GetMethod("<Clone>$") is null)
        {
            return null;
        }

        Type[] single =
        [
            .. type.GetConstructors()
                .Select(constructor => constructor.GetParameters())
                .Where(parameters => parameters.Length == 1 && parameters[0].ParameterType != type)
                .Select(parameters => parameters[0].ParameterType),
        ];

        return single.Length == 1 ? single[0] : null;
    }
}
