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

    private static readonly JsonSerializerOptions camelCase = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    private static readonly MethodInfo wrap = typeof(ValueObjects).GetMethod(nameof(ValueObjects.Wrap))!;

    /// <summary>Whether the request's one constructor takes only the target's <c>Id</c>.</summary>
    public static bool IsIdOnly(Type requestType) =>
        ConstructorOf(requestType).GetParameters() is [{ } only] && string.Equals(only.Name, IdParameter, StringComparison.OrdinalIgnoreCase);

    /// <summary>A request whose constructor takes exactly the target id, built from the id's Guid.</summary>
    public static Func<Guid, object> ForId(Type requestType)
    {
        ConstructorInfo constructor = ConstructorOf(requestType);

        if (!IsIdOnly(requestType))
        {
            throw new InvalidOperationException(
                $"{requestType.FullName} cannot be built from an id alone: its constructor is ({Describe(constructor)}), not (Id).");
        }

        ParameterExpression id = Expression.Parameter(typeof(Guid), "id");
        Type idType = constructor.GetParameters()[0].ParameterType;
        Expression wrapped = Expression.Convert(
            Expression.Call(wrap, Expression.Constant(idType), Expression.Convert(id, typeof(object))), idType);

        return Expression.Lambda<Func<Guid, object>>(Expression.Convert(Expression.New(constructor, wrapped), typeof(object)), id).Compile();
    }

    /// <summary>
    /// A request built from the target id (ignored when the request has no <c>Id</c>, as a create) and form values keyed by
    /// parameter name; a primitive is wrapped into the parameter's type, a missing value is the type's default.
    /// </summary>
    public static Func<Guid, IReadOnlyDictionary<string, object?>, object> ForForm(Type requestType)
    {
        ConstructorInfo constructor = ConstructorOf(requestType);
        ParameterInfo[] parameters = constructor.GetParameters();

        return (id, values) =>
        {
            object?[] arguments = new object?[parameters.Length];

            for (int index = 0; index < parameters.Length; index++)
            {
                ParameterInfo parameter = parameters[index];

                arguments[index] = string.Equals(parameter.Name, IdParameter, StringComparison.OrdinalIgnoreCase)
                    ? ValueObjects.Wrap(parameter.ParameterType, id)
                    : ValueObjects.Wrap(parameter.ParameterType, ValueOf(values, parameter.Name!))
                        ?? DefaultOf(parameter.ParameterType);
            }

            return constructor.Invoke(arguments);
        };
    }

    /// <summary>A list query built from a page's <see cref="ListQueryInput"/>.</summary>
    public static Func<ListQueryInput, object> ForList(Type listQueryType)
    {
        ConstructorInfo constructor = ConstructorOf(listQueryType);
        Type[] supported = [typeof(Paggination), typeof(Term), typeof(Filtering), typeof(Sorting)];
        ParameterInfo[] parameters = constructor.GetParameters();

        if (!parameters.Any(parameter => parameter.ParameterType == typeof(Paggination))
            || parameters.Any(parameter => !supported.Contains(parameter.ParameterType)))
        {
            throw new InvalidOperationException(
                $"{listQueryType.FullName} is not a list query a view can build: its constructor is ({Describe(constructor)}), "
                + "which must take a Paggination and may take a Term, Filtering and Sorting.");
        }

        return input =>
        {
            Filtering? filtering = input.Filtering.Count == 0
                ? null
                : Filtering.Create(JsonSerializer.Serialize(
                    input.Filtering.Select(filter => new { filter.Id, filter.Value, filter.Mode }), camelCase));
            Sorting? sorting = input.Sorting.Count == 0 ? null : Sorting.Create(JsonSerializer.Serialize(input.Sorting, camelCase));
            Term? term = input.Term is { Length: > 0 } text ? new Term(text) : null;

            return constructor.Invoke(
            [
                .. parameters.Select(parameter => parameter.ParameterType switch
                {
                    _ when parameter.ParameterType == typeof(Paggination) => input.Paggination,
                    _ when parameter.ParameterType == typeof(Term) => term,
                    _ when parameter.ParameterType == typeof(Filtering) => filtering,
                    _ => (object?)sorting,
                }),
            ]);
        };
    }

    private static object? ValueOf(IReadOnlyDictionary<string, object?> values, string name)
    {
        if (values.TryGetValue(name, out object? exact))
        {
            return exact;
        }

        return values.FirstOrDefault(pair => string.Equals(pair.Key, name, StringComparison.OrdinalIgnoreCase)).Value;
    }

    private static object? DefaultOf(Type type) => type.IsValueType ? Activator.CreateInstance(type) : null;

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
