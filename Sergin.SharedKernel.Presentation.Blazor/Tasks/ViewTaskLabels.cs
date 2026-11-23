using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Presentation.Blazor.Aggregates;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks;

/// <summary>
/// <see cref="ITaskLabels"/> from the view registry: the label of the view command whose request is the task, as the
/// activity indicator shows it. The registry is the UI host's; a host without one names no task.
/// </summary>
internal sealed class ViewTaskLabels(IServiceProvider services, TaskCatalog catalog, IStringLocalizer localizer) : ITaskLabels
{
    public string? LabelOf(TaskDescriptor task)
    {
        ArgumentNullException.ThrowIfNull(task);

        return services.GetService<AggregateViewRegistry>() is { } views
            && ActivityRows.CommandLabel(task.Name, catalog, views) is { } label
                ? localizer.Text(label)
                : null;
    }
}
