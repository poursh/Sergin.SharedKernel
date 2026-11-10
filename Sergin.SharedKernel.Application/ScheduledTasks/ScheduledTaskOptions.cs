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

    /// <summary>The engine's wait before each retry, in seconds; the last value repeats.</summary>
    public IReadOnlyList<int> RetryIntervalsSeconds { get; set; } = [30, 120];

    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(30);

    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromHours(1);

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

        failure = string.Empty;
        return true;
    }
}
