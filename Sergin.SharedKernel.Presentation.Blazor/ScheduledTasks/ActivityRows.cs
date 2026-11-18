using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Application.ScheduledTasks.Activity;
using Sergin.SharedKernel.Application.ScheduledTasks.Commands;
using Sergin.SharedKernel.Presentation.Localizations;

namespace Sergin.SharedKernel.Presentation.Blazor.ScheduledTasks;

/// <summary>Where a run of the activity indicator stands.</summary>
internal enum ActivityState
{
    Running,
    Succeeded,
    PartlyFailed,
    Failed,
}

/// <summary>
/// One run as the activity indicator shows it: the command's view label (null when no view names it, and
/// <paramref name="CommandName"/> shows instead), the target's stored label or a record count, the target's detail link
/// for a one-record root target, where the run stands, and a one-record run's first error as stored.
/// </summary>
internal sealed record ActivityRow(
    BackgroundRunRef Ref,
    TextDefinition? CommandLabel,
    string CommandName,
    string Target,
    string? Href,
    ActivityState State,
    DateTime StartedAtUtc,
    DateTime? CompletedAtUtc,
    int Done,
    int Total,
    string? FirstErrorCode,
    string? FirstErrorDescription)
{
    public bool IsRunning => State == ActivityState.Running;
}

/// <summary>Turns the activity read into rows: pure, so it is tested without a browser.</summary>
internal static class ActivityRows
{
    public static IReadOnlyList<ActivityRow> Build(
        IReadOnlyList<ActivityItem> items, ScheduledCommandCatalog catalog, AggregateViewRegistry views, Func<int, string> recordsText)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(views);
        ArgumentNullException.ThrowIfNull(recordsText);

        return [.. items.Select(item => Row(item, catalog, views, recordsText))];
    }

    /// <summary>"1 record" or "{Count} records", in the current culture: what a run with no stored label is named by.</summary>
    public static Func<int, string> RecordsText(IStringLocalizer localizer, ICultureFormatter formatter)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(formatter);

        return count => count == 1
            ? localizer.Text(ScheduledTaskUiTexts.Activity.OneRecord)
            : localizer.Text(ScheduledTaskUiTexts.Activity.Records, new { Count = formatter.Number(count) });
    }

    public static ActivityState StateOf(ActivityItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (item.Unfinished > 0)
        {
            return ActivityState.Running;
        }

        if (item.Failed == 0)
        {
            return ActivityState.Succeeded;
        }

        return item.Succeeded == 0 ? ActivityState.Failed : ActivityState.PartlyFailed;
    }

    private static ActivityRow Row(ActivityItem item, ScheduledCommandCatalog catalog, AggregateViewRegistry views, Func<int, string> recordsText)
    {
        ScheduledCommand? command = catalog.TryGet(item.CommandName, out ScheduledCommand? found) ? found : null;
        AggregateViewModel? model = command is null ? null : views.Find(command.Root);

        return new ActivityRow(
            item.Ref,
            LabelOf(command, model),
            item.CommandName,
            item.TargetLabel ?? recordsText(item.Total),
            HrefOf(item, command, model),
            StateOf(item),
            item.StartedAtUtc,
            item.CompletedAtUtc,
            item.Succeeded + item.Failed,
            item.Total,
            item.FirstErrorCode,
            item.FirstErrorDescription);
    }

    private static TextDefinition? LabelOf(ScheduledCommand? command, AggregateViewModel? model)
    {
        if (command is null || model is null)
        {
            return null;
        }

        return model.Commands.FirstOrDefault(view => view.Request == command.RequestType)?.Label
            ?? model.Children.SelectMany(child => child.Commands).FirstOrDefault(view => view.Request == command.RequestType)?.Label;
    }

    // A link only for one record of the root itself: a child target's page needs its root, and many targets have no one page.
    private static string? HrefOf(ActivityItem item, ScheduledCommand? command, AggregateViewModel? model) =>
        item.Total == 1 && item.TargetId is { } target && command?.TargetKind == CommandTargetKind.Entity && model is not null
            ? model.Routes.Detail?.Invoke(target)
            : null;
}
