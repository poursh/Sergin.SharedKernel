using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>The scheduled-task texts, under <c>common.scheduled-tasks.*</c>; collected by <c>TextSource.Common</c>.</summary>
public static class ScheduledTaskTexts
{
    public static readonly TextCategory Category = TextCategory.Create("Common", "ScheduledTasks");

    public static readonly TextDefinition Singular = Category.Text("Singular", "Scheduled task");
}
