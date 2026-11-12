using System.Globalization;
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
    /// <paramref name="primitive"/> as <paramref name="type"/>: a wrapper is constructed around the (recursively wrapped)
    /// primitive; a value already of the type, or null, is returned as it is; a primitive of another kind is converted.
    /// </summary>
    public static object? Wrap(Type type, object? primitive)
    {
        ArgumentNullException.ThrowIfNull(type);

        Type target = Nullable.GetUnderlyingType(type) ?? type;

        if (primitive is null || target.IsInstanceOfType(primitive))
        {
            return primitive;
        }

        if (target.IsEnum)
        {
            return primitive is string name ? Enum.Parse(target, name) : Enum.ToObject(target, primitive);
        }

        if (target == typeof(Guid) && primitive is string text)
        {
            return Guid.Parse(text);
        }

        if (IsPrimitive(target))
        {
            return Convert.ChangeType(primitive, target, CultureInfo.InvariantCulture);
        }

        if (WrappedValueOf(target) is { } inner)
        {
            ConstructorInfo constructor = target.GetConstructors()
                .Single(candidate => candidate.GetParameters() is [{ } only] && only.ParameterType == inner);

            return constructor.Invoke([Wrap(inner, primitive)]);
        }

        throw new InvalidOperationException(
            $"{target.FullName} is neither a primitive nor a single-value wrapper, so {primitive.GetType().Name} cannot become one.");
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

            ParameterInfo parameter = type.GetConstructors().Single(candidate => candidate.GetParameters().Length == 1).GetParameters()[0];
            PropertyInfo property = type.GetProperty(parameter.Name!, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new InvalidOperationException($"{type.FullName} has no property for its constructor value '{parameter.Name}'.");

            current = property.GetValue(current);
        }

        return current;
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
