using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Sergin.SharedKernel.Application.Commands.Queries;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>What a list page asks of a list query: paging, the quick-search term, the applied filters, the sort.</summary>
public sealed record ListQueryInput(
    Paggination Paggination,
    string? Term,
    IReadOnlyList<FilterData> Filtering,
    IReadOnlyList<SortingData> Sorting);

/// <summary>
/// Builds a request record from what a view has: an id, form values by parameter name, or a list input. The
/// constructor is read once per request type, when the factory delegate is made.
/// </summary>
public static class RequestFactory
{
    private const string IdParameter = "Id";
    private const string RootIdParameter = "RootId";

    private static readonly Dictionary<string, object?> noValues = [];

    private static readonly JsonSerializerOptions camelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Whether the request's one constructor takes only the target's <c>Id</c>.</summary>
    public static bool IsIdOnly(Type requestType) =>
        ConstructorOf(requestType).GetParameters() is [{ } only] && string.Equals(only.Name, IdParameter, StringComparison.OrdinalIgnoreCase);

    /// <summary>Whether the request's one constructor takes only a child's <c>RootId</c> and <c>Id</c>, in either order.</summary>
    public static bool IsChildIdOnly(Type requestType) =>
        ConstructorOf(requestType).GetParameters() is [{ } first, { } second]
        && (IsNamed(first, RootIdParameter) && IsNamed(second, IdParameter) || IsNamed(first, IdParameter) && IsNamed(second, RootIdParameter));

    /// <summary>Whether the request's one constructor takes nothing (an overview query).</summary>
    public static bool IsParameterless(Type requestType) => ConstructorOf(requestType).GetParameters().Length == 0;

    /// <summary>A request whose constructor takes nothing.</summary>
    public static Func<object> ForNone(Type requestType)
    {
        ConstructorInfo constructor = ConstructorOf(requestType);

        if (!IsParameterless(requestType))
        {
            throw new InvalidOperationException(
                $"{requestType.FullName} cannot be built without arguments: its constructor is ({Describe(constructor)}).");
        }

        Func<object?[], object> build = Compile(constructor);

        return () => build([]);
    }

    /// <summary>A request whose constructor takes exactly the target id, built from the id's Guid.</summary>
    public static Func<Guid, object> ForId(Type requestType)
    {
        ConstructorInfo constructor = ConstructorOf(requestType);

        if (!IsIdOnly(requestType))
        {
            throw new InvalidOperationException(
                $"{requestType.FullName} cannot be built from an id alone: its constructor is ({Describe(constructor)}), not (Id).");
        }

        Func<object?, object?> id = ValueObjects.Converter(constructor.GetParameters()[0].ParameterType, neverNull: true);
        Func<object?[], object> build = Compile(constructor);

        return guid => build([id(guid)]);
    }

    /// <summary>A request whose constructor takes exactly a child's <c>RootId</c> and <c>Id</c>, built from the two Guids (root first).</summary>
    public static Func<Guid, Guid, object> ForChildId(Type requestType)
    {
        ConstructorInfo constructor = ConstructorOf(requestType);

        if (!IsChildIdOnly(requestType))
        {
            throw new InvalidOperationException(
                $"{requestType.FullName} cannot be built from a root and child id alone: its constructor is ({Describe(constructor)}), not (RootId, Id).");
        }

        Func<Guid, Guid, IReadOnlyDictionary<string, object?>, object> build = ForChildForm(requestType);

        return (rootId, id) => build(rootId, id, noValues);
    }

    /// <summary>
    /// A request built from the target id (ignored when the request has no <c>Id</c>, as a create) and form values keyed by
    /// parameter name; a primitive is wrapped into the parameter's type. A missing value becomes the primitive's default
    /// (empty string, <see cref="Guid.Empty"/>, 0), so a wrapper is never null and the validators can read it; only a
    /// parameter declared nullable stays null.
    /// </summary>
    public static Func<Guid, IReadOnlyDictionary<string, object?>, object> ForForm(Type requestType)
    {
        Func<Guid, Guid, IReadOnlyDictionary<string, object?>, object> build = ForChildForm(requestType);

        return (id, values) => build(Guid.Empty, id, values);
    }

    /// <summary>
    /// A request of a child entity built like <see cref="ForForm"/>, from the root's id (the <c>RootId</c> parameter), the
    /// child's id (the <c>Id</c> parameter, ignored when the request has none, as an add) and form values.
    /// </summary>
    public static Func<Guid, Guid, IReadOnlyDictionary<string, object?>, object> ForChildForm(Type requestType)
    {
        ConstructorInfo constructor = ConstructorOf(requestType);
        ParameterInfo[] parameters = constructor.GetParameters();
        NullabilityInfoContext nullability = new();
        Func<object?, object?>[] converters =
        [
            .. parameters.Select(parameter => ValueObjects.Converter(
                parameter.ParameterType,
                neverNull: !IsNullable(parameter, nullability))),
        ];
        bool[] isId = [.. parameters.Select(parameter => IsNamed(parameter, IdParameter))];
        bool[] isRootId = [.. parameters.Select(parameter => IsNamed(parameter, RootIdParameter))];
        string[] names = [.. parameters.Select(parameter => parameter.Name!)];
        Func<object?[], object> build = Compile(constructor);

        return (rootId, id, values) =>
        {
            object?[] arguments = new object?[names.Length];

            for (int index = 0; index < names.Length; index++)
            {
                object? value = ValueOf(values, names[index]);

                if (isId[index])
                {
                    value = id;
                }
                else if (isRootId[index])
                {
                    value = rootId;
                }

                arguments[index] = converters[index](value);
            }

            return build(arguments);
        };
    }

    /// <summary>A request of a child built from the root's id and form values, as an add command whose constructor takes the <c>RootId</c>.</summary>
    public static Func<Guid, IReadOnlyDictionary<string, object?>, object> ForRootForm(Type requestType)
    {
        Func<Guid, Guid, IReadOnlyDictionary<string, object?>, object> build = ForChildForm(requestType);

        return (rootId, values) => build(rootId, Guid.Empty, values);
    }

    /// <summary>A list query built from a page's <see cref="ListQueryInput"/>.</summary>
    public static Func<ListQueryInput, object> ForList(Type listQueryType)
    {
        Func<Guid, ListQueryInput, object> build = ForListCore(listQueryType, rootIdType: null);

        return input => build(Guid.Empty, input);
    }

    /// <summary>
    /// A child list query (<c>IChildListOf</c>) built from its root's id and a <see cref="ListQueryInput"/>: its
    /// constructor takes the root id (of <paramref name="rootIdType"/>) besides what a list query takes.
    /// </summary>
    public static Func<Guid, ListQueryInput, object> ForChildList(Type listQueryType, Type rootIdType)
    {
        ArgumentNullException.ThrowIfNull(rootIdType);

        return ForListCore(listQueryType, rootIdType);
    }

    private static Func<Guid, ListQueryInput, object> ForListCore(Type listQueryType, Type? rootIdType)
    {
        ConstructorInfo constructor = ConstructorOf(listQueryType);
        Type[] supported = [typeof(Paggination), typeof(Term), typeof(Filtering), typeof(Sorting)];
        ParameterInfo[] parameters = constructor.GetParameters();
        int rootIds = rootIdType is null ? 0 : parameters.Count(parameter => parameter.ParameterType == rootIdType);

        if (!parameters.Any(parameter => parameter.ParameterType == typeof(Paggination))
            || parameters.Any(parameter => !supported.Contains(parameter.ParameterType) && parameter.ParameterType != rootIdType)
            || rootIdType is not null && rootIds != 1)
        {
            string rootId = rootIdType is null ? string.Empty : $"its root's {rootIdType.Name} and ";

            throw new InvalidOperationException(
                $"{listQueryType.FullName} is not a list query a view can build: its constructor is ({Describe(constructor)}), "
                + $"which must take {rootId}a Paggination and may take a Term, Filtering and Sorting.");
        }

        Func<object?, object?> toRootId = rootIdType is null ? _ => null : ValueObjects.Converter(rootIdType, neverNull: true);
        Func<object?[], object> build = Compile(constructor);

        return (rootId, input) =>
        {
            Filtering? filtering = input.Filtering.Count == 0
                ? null
                : Filtering.Create(JsonSerializer.Serialize(
                    input.Filtering.Select(filter => new { filter.Id, filter.Value, filter.Mode }), camelCase));
            Sorting? sorting = input.Sorting.Count == 0 ? null : Sorting.Create(JsonSerializer.Serialize(input.Sorting, camelCase));
            Term? term = input.Term is { Length: > 0 } text ? new Term(text) : null;

            return build(
            [
                .. parameters.Select(parameter => parameter.ParameterType switch
                {
                    _ when parameter.ParameterType == typeof(Paggination) => input.Paggination,
                    _ when parameter.ParameterType == typeof(Term) => term,
                    _ when parameter.ParameterType == typeof(Filtering) => filtering,
                    _ when parameter.ParameterType == typeof(Sorting) => sorting,
                    _ => toRootId(rootId),
                }),
            ]);
        };
    }

    private static bool IsNamed(ParameterInfo parameter, string name) => string.Equals(parameter.Name, name, StringComparison.OrdinalIgnoreCase);

    private static object? ValueOf(IReadOnlyDictionary<string, object?> values, string name)
    {
        if (values.TryGetValue(name, out object? exact))
        {
            return exact;
        }

        return values.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static bool IsNullable(ParameterInfo parameter, NullabilityInfoContext nullability) =>
        Nullable.GetUnderlyingType(parameter.ParameterType) is not null
        || !parameter.ParameterType.IsValueType && nullability.Create(parameter).WriteState == NullabilityState.Nullable;

    /// <summary>The constructor as a compiled delegate over an argument array, so building a request reflects on nothing.</summary>
    private static Func<object?[], object> Compile(ConstructorInfo constructor)
    {
        ParameterExpression arguments = Expression.Parameter(typeof(object?[]), "arguments");
        Expression[] converted =
        [
            .. constructor.GetParameters().Select((parameter, index) =>
                Expression.Convert(Expression.ArrayIndex(arguments, Expression.Constant(index)), parameter.ParameterType)),
        ];

        return Expression
            .Lambda<Func<object?[], object>>(Expression.Convert(Expression.New(constructor, converted), typeof(object)), arguments)
            .Compile();
    }

    private static string Describe(ConstructorInfo constructor) =>
        string.Join(", ", constructor.GetParameters().Select(parameter => $"{parameter.ParameterType.Name} {parameter.Name}"));

    private static ConstructorInfo ConstructorOf(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        ConstructorInfo[] constructors = requestType.GetConstructors();

        return constructors.Length == 1
            ? constructors[0]
            : throw new InvalidOperationException(
                $"{requestType.FullName} must have exactly one public constructor for a view to build it; it has {constructors.Length}.");
    }
}
