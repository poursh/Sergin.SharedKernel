using MudBlazor;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Tasks;
using Sergin.SharedKernel.Application.Tasks.Queries;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Modules;
using Sergin.SharedKernel.Presentation.Blazor.Modules;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks.Runs;

/// <summary>What the Task runs page says about its modules and each run: pure, so it is tested without a browser.</summary>
internal static class TaskRunRows
{
    /// <summary>
    /// The modules whose runs the page lists: the distinct schemas of the task catalogue, in the drawer's module order,
    /// then any schema no UI module has, by ordinal. Empty when no module runs tasks.
    /// </summary>
    public static IReadOnlyList<string> Modules(TaskCatalog tasks, SerginUiModuleCatalog ui)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(ui);

        HashSet<string> schemas = [.. tasks.Commands.Select(task => task.Schema).OfType<string>()];
        List<string> ordered = [];

        foreach (ISerginWebUiModule module in ui.Modules)
        {
            if (schemas.Remove(module.Schema))
            {
                ordered.Add(module.Schema);
            }
        }

        ordered.AddRange(schemas.Order(StringComparer.Ordinal));

        return ordered;
    }

    /// <summary>
    /// What started a run, under the person: the trigger and, for a schedule's run, its cron as stored (schedules have no
    /// name; null when the run has no schedule, or its schedule is gone).
    /// </summary>
    public static (TextDefinition Trigger, string? Schedule) StartedByLine(TaskRunsListItem run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return run.Trigger switch
        {
            TaskRunTrigger.Manual => (TaskUiTexts.RunList.TriggerManual, null),
            TaskRunTrigger.Once => (TaskUiTexts.RunList.TriggerOnce, null),
            TaskRunTrigger.ScheduleNow => (TaskUiTexts.RunList.TriggerScheduleNow, ScheduleText(run)),
            _ => (TaskUiTexts.RunList.TriggerSchedule, ScheduleText(run)),
        };
    }

    // The schedule the way the schedules section shows it: the cron as stored, then its time zone.
    private static string? ScheduleText(TaskRunsListItem run)
    {
        if (run.ScheduleCron is null)
        {
            return null;
        }

        return run.ScheduleTimeZoneId is { } zone ? $"{run.ScheduleCron} {zone}" : run.ScheduleCron;
    }

    /// <summary>The trigger line in the current culture: "Schedule · 0 9 * * * UTC", or the trigger alone.</summary>
    public static string TriggerLine(IStringLocalizer localizer, TaskRunsListItem run)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        (TextDefinition trigger, string? schedule) = StartedByLine(run);

        return schedule is null
            ? localizer.Text(trigger)
            : localizer.Text(TaskUiTexts.RunList.TriggerWithSchedule, new { Trigger = localizer.Text(trigger), Schedule = schedule });
    }

    /// <summary>The text a trigger is offered by in the filter.</summary>
    public static TextDefinition TriggerText(TaskRunTrigger trigger) =>
        trigger switch
        {
            TaskRunTrigger.Manual => TaskUiTexts.RunList.TriggerManual,
            TaskRunTrigger.Once => TaskUiTexts.RunList.TriggerOnce,
            TaskRunTrigger.ScheduleNow => TaskUiTexts.RunList.TriggerScheduleNow,
            _ => TaskUiTexts.RunList.TriggerSchedule,
        };

    /// <summary>
    /// What a run ran on, as the activity row names it: a record's stored label; else, for a collection task's run, its
    /// scope (<paramref name="scopeName"/>); else a record count (<paramref name="recordsText"/>).
    /// </summary>
    public static string RecordsText(TaskRunsListItem run, Func<int, string> recordsText, Func<Guid, string> scopeName)
    {
        ArgumentNullException.ThrowIfNull(run);
        ArgumentNullException.ThrowIfNull(recordsText);
        ArgumentNullException.ThrowIfNull(scopeName);

        if (run.TargetLabel is { } label)
        {
            return label;
        }

        return run.ScopeId is { } scope ? scopeName(scope) : recordsText(run.Total);
    }

    /// <summary>
    /// A finished run's counts, under its outcome: how many records succeeded and failed, and how many were cancelled
    /// when any were. No status word, since the outcome above already says it.
    /// </summary>
    public static string Summary(IStringLocalizer localizer, ICultureFormatter formatter, TaskRunsListItem run)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(formatter);
        ArgumentNullException.ThrowIfNull(run);

        var values = new
        {
            Succeeded = formatter.Number(run.Succeeded),
            Total = formatter.Number(run.Total),
            Failed = formatter.Number(run.Failed),
            Cancelled = formatter.Number(run.Cancelled),
        };

        return localizer.Text(run.Cancelled > 0 ? TaskUiTexts.RunList.SummaryWithCancelled : TaskUiTexts.RunList.Summary, values);
    }

    /// <summary>
    /// The colour of a run's status chip: blue while running, green when every record succeeded, amber when some
    /// failed, red when all failed, neutral for a cancelled or skipped run.
    /// </summary>
    public static Color OutcomeColor(RunOutcome outcome) => outcome switch
    {
        RunOutcome.Running => Color.Info,
        RunOutcome.Succeeded => Color.Success,
        RunOutcome.PartlyFailed => Color.Warning,
        RunOutcome.Failed => Color.Error,
        _ => Color.Default,
    };

    /// <summary>
    /// The aggregate a task acts on, by its declared name in the current culture, else the root type's name; a dash for a
    /// task the catalogue no longer knows.
    /// </summary>
    public static string AggregateText(IStringLocalizer localizer, string commandName, TaskCatalog catalog, AggregateFeatureRegistry aggregates)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        return catalog.ByName(commandName) is { } task ? RootText(localizer, task.Root, aggregates) : "—";
    }

    /// <summary>An aggregate root by its declared name in the current culture, else its type's name.</summary>
    public static string RootText(IStringLocalizer localizer, Type root, AggregateFeatureRegistry aggregates)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(root);
        ArgumentNullException.ThrowIfNull(aggregates);

        return aggregates.NameOf(root) is { } name ? localizer.Text(name) : root.Name;
    }

    /// <summary>The task's view label, else its stored name.</summary>
    public static string TaskText(IStringLocalizer localizer, string commandName, TaskCatalog catalog, AggregateViewRegistry views)
    {
        ArgumentNullException.ThrowIfNull(localizer);

        return ActivityRows.CommandLabel(commandName, catalog, views) is { } label ? localizer.Text(label) : commandName;
    }
}
