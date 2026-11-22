namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// The name a person knows a task by, in the current culture: the label of the view command that starts it. Views are a
/// presentation's, so a presentation registers this; with none registered, a refusal names the task by its task name.
/// </summary>
public interface ITaskLabels
{
    /// <summary>The task's label; null when no view names it.</summary>
    string? LabelOf(TaskDescriptor task);
}
