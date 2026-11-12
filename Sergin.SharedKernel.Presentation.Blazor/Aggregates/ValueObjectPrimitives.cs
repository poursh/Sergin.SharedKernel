namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// The primitive a command field's type carries: a primitive, <see cref="string"/>, an enum or a date/time/Guid type
/// is itself; a record wrapping one value (one public constructor parameter, <c>DeviceId(string Value)</c>) is that
/// value's primitive; anything else has none. Nullable types are read through.
/// </summary>
internal static class ValueObjectPrimitives
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

            if (current.IsPrimitive || current.IsEnum || knownPrimitives.Contains(current))
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

    public static bool IsNumber(Type primitive) => numbers.Contains(primitive);

    public static bool IsDate(Type primitive) => dates.Contains(primitive);

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
