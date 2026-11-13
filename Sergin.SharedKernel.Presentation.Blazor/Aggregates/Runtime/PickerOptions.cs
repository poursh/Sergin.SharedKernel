using System.Collections;
using System.Linq.Expressions;
using System.Reflection;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>One record a picker offers: its id and what shows for it, data as stored.</summary>
public sealed record PickerOption(Guid Id, string Label);

/// <summary>
/// Where a picker's options come from: a root's list query (<c>IListOf</c>), or a child list query (<c>IChildListOf</c>)
/// under a chosen root. Each option is a list item's <c>Id</c> and its first text property (a <see cref="string"/>, or a
/// value object wrapping one), its display field as stored. The query, its builder and the readers are made once, when
/// the registry is built; loading reflects on nothing.
/// </summary>
public sealed class PickerOptions
{
    /// <summary>The most options a picker loads: the same ceiling as the hand-written pickers, tens of rows today.</summary>
    public const int MaxOptions = 200;

    private readonly RequestInvoker invoker;
    private readonly Func<Guid, ListQueryInput, object> build;
    private readonly Func<object, IEnumerable> data;
    private readonly Func<object, object?> id;
    private readonly Func<object, object?>? label;

    private PickerOptions(Type listQuery, Type item, Func<Guid, ListQueryInput, object> build, bool child)
    {
        invoker = RequestInvoker.For(listQuery);
        this.build = build;
        data = DataReaderOf(item);

        PropertyInfo idProperty = item.GetProperty("Id", BindingFlags.Public | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"{item.FullName}, the item of {listQuery.FullName}, has no Id for a picker to offer.");
        id = ValueObjects.UnwrappedGetter(idProperty);

        PropertyInfo? labelProperty = item.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(property => property.MetadataToken)
            .FirstOrDefault(property => property.Name != "Id" && ValueObjects.PrimitiveOf(property.PropertyType) == typeof(string));
        label = labelProperty is null ? null : ValueObjects.UnwrappedGetter(labelProperty);

        IsChild = child;
    }

    /// <summary>Whether the options are a child list's, loaded under a chosen root.</summary>
    public bool IsChild { get; }

    /// <summary>The options of a root's list query.</summary>
    public static PickerOptions ForRoot(Type listQuery)
    {
        Func<ListQueryInput, object> build = RequestFactory.ForList(listQuery);

        return new PickerOptions(listQuery, ItemOf(listQuery), (_, input) => build(input), child: false);
    }

    /// <summary>The options of a child list query, whose constructor takes its root's id of <paramref name="rootIdType"/>.</summary>
    public static PickerOptions ForChild(Type childListQuery, Type rootIdType) =>
        new(childListQuery, ItemOf(childListQuery), RequestFactory.ForChildList(childListQuery, rootIdType), child: true);

    /// <summary>The first <see cref="MaxOptions"/> options; for a child list, those under <paramref name="parentId"/>.</summary>
    public async Task<ErrorOr<IReadOnlyList<PickerOption>>> LoadAsync(
        ISerginDispatcher dispatcher, Guid parentId = default, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dispatcher);

        object request = build(parentId, new ListQueryInput(Paggination.Create(MaxOptions, 1), Term: null, Filtering: [], Sorting: []));
        ViewSendResult result = await invoker.SendAsync(dispatcher, request, cancellationToken);

        if (result.IsError)
        {
            List<Error> errors = [.. result.Errors];
            return errors;
        }

        List<PickerOption> options = [];

        foreach (object item in data(result.Value!))
        {
            var optionId = (Guid)id(item)!;
            options.Add(new PickerOption(optionId, label?.Invoke(item)?.ToString() ?? optionId.ToString()));
        }

        return options;
    }

    // The response's rows, compiled once: (object response) => (IEnumerable)((ListQueryResponse<TItem>)response).Data.
    private static Func<object, IEnumerable> DataReaderOf(Type item)
    {
        Type responseType = typeof(ListQueryResponse<>).MakeGenericType(item);
        ParameterExpression response = Expression.Parameter(typeof(object), "response");
        Expression rows = Expression.Property(Expression.Convert(response, responseType), nameof(ListQueryResponse<object>.Data));

        return Expression.Lambda<Func<object, IEnumerable>>(Expression.Convert(rows, typeof(IEnumerable)), response).Compile();
    }

    private static Type ItemOf(Type listQuery) =>
        AggregateViewModelJoin.ListItemOf(listQuery)
        ?? throw new InvalidOperationException($"{listQuery.FullName} is not an IListQuery<TItem>, so a picker cannot read its items.");
}
