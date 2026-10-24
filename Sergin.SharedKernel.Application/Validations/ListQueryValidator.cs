using FluentValidation;
using Sergin.SharedKernel.Application.Commands.Queries;

namespace Sergin.SharedKernel.Application.Validations;

/// <summary>
/// The validator a list feature derives from: every sort and filter must name a field of the feature's
/// <see cref="ListFieldSet"/>, in a mode that field allows, with a value of the field's kind. Anything else is a
/// validation error naming <c>Sorting</c> or <c>Filtering</c>, before a query repository sees it.
/// </summary>
/// <remarks>
/// The query repository's column map is keyed by the same set, so a request this validator passes is one the
/// repository can translate. Paging needs no rule: <see cref="Paggination"/> guards itself at construction.
/// </remarks>
public abstract class ListQueryValidator<TQuery> : AbstractValidator<TQuery>
    where TQuery : ListQuery
{
    protected ListQueryValidator(ListFieldSet fields)
    {
        ArgumentNullException.ThrowIfNull(fields);

        RuleFor(query => query.Sorting).Custom((sorting, context) =>
        {
            foreach (SortingData sort in sorting?.Sorts ?? [])
            {
                if (fields.Find(sort.Id) is not { Sortable: true })
                {
                    context.AddFailure(nameof(ListQuery.Sorting), $"'{sort.Id}' is not a sortable field.");
                }
            }
        });

        RuleFor(query => query.Filtering).Custom((filtering, context) =>
        {
            foreach (FilterData filter in filtering?.Filters ?? [])
            {
                if (Describe(filter, fields) is { } failure)
                {
                    context.AddFailure(nameof(ListQuery.Filtering), failure);
                }
            }
        });
    }

    private static string? Describe(FilterData filter, ListFieldSet fields)
    {
        if (fields.Find(filter.Id) is not { } field)
        {
            return $"'{filter.Id}' is not a filterable field.";
        }

        if (!Enum.TryParse(filter.Mode, out FilteringType mode) || !field.FilterModes.Contains(mode))
        {
            return $"'{filter.Id}' cannot be filtered with '{filter.Mode}'.";
        }

        return ListFilterValues.TryParse(filter, field, out _)
            ? null
            : $"'{filter.Id}' has a value that is not a valid {field.Kind}.";
    }
}
