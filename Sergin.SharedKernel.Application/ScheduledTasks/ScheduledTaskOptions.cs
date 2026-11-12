namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// The scheduled tasks' tuning, bound from <c>Sergin:ScheduledTasks</c>: how often a run item that threw is tried,
/// how long the engine waits between tries, and how long finished runs are kept.
/// </summary>
public sealed class ScheduledTaskOptions
{
    public const string SectionName = "ScheduledTasks";

    /// <summary>Attempts a run item gets when it throws, the first included; an ErrorOr error is never retried.</summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>The intervals used when none are configured.</summary>
    public static IReadOnlyList<int> DefaultRetryIntervalsSeconds { get; } = [30, 120];

    /// <summary>
    /// The engine's wait before each retry, in seconds; the last value repeats. Empty until bound: the configuration
    /// binder appends configured items to a collection's existing ones, so the default is applied afterwards, by
    /// <see cref="ScheduledTaskOptionsBuilderExtensions.WithDefaults"/>, only when nothing was configured.
    /// </summary>
    public IReadOnlyList<int> RetryIntervalsSeconds { get; set; } = [];

    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// How long an unfinished run item may go without activity before the recovery takes its job for lost and hands it
    /// to the engine again; a Running item that quiet may be claimed by the new delivery. Longer than any retry wait (so a
    /// healthy retry is never duplicated) and longer than the slowest command (or it would be sent twice).
    /// </summary>
    public TimeSpan StaleAfter { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>How often the recovery looks for stale run items.</summary>
    public TimeSpan RecoveryInterval { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>Names the first key scheduled tasks could not run with, the way <c>OutboxOptions.Validate</c> does.</summary>
    public bool Validate(out string failure)
    {
        if (MaxAttempts < 1)
        {
            failure = $"Sergin:{SectionName}:{nameof(MaxAttempts)} must be at least 1.";
            return false;
        }

        if (RetryIntervalsSeconds is null || RetryIntervalsSeconds.Count == 0 || RetryIntervalsSeconds.Any(seconds => seconds < 1))
        {
            failure = $"Sergin:{SectionName}:{nameof(RetryIntervalsSeconds)} must list at least one interval, each at least 1 second.";
            return false;
        }

        if (Retention <= TimeSpan.Zero)
        {
            failure = $"Sergin:{SectionName}:{nameof(Retention)} must be a positive duration.";
            return false;
        }

        if (PurgeInterval <= TimeSpan.Zero)
        {
            failure = $"Sergin:{SectionName}:{nameof(PurgeInterval)} must be a positive duration.";
            return false;
        }

        if (StaleAfter <= TimeSpan.Zero)
        {
            failure = $"Sergin:{SectionName}:{nameof(StaleAfter)} must be a positive duration.";
            return false;
        }

        if (RecoveryInterval <= TimeSpan.Zero)
        {
            failure = $"Sergin:{SectionName}:{nameof(RecoveryInterval)} must be a positive duration.";
            return false;
        }

        // A Retrying item is quiet for its whole wait; the recovery must not take that for a lost job.
        var longestRetry = TimeSpan.FromSeconds(RetryIntervalsSeconds.Max());

        if (StaleAfter - longestRetry < RecoveryInterval)
        {
            failure = $"Sergin:{SectionName}:{nameof(StaleAfter)} must exceed the longest Sergin:{SectionName}:{nameof(RetryIntervalsSeconds)} "
                + $"value by at least Sergin:{SectionName}:{nameof(RecoveryInterval)}.";
            return false;
        }

        failure = string.Empty;
        return true;
    }
}
