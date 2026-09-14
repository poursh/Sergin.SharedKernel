namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// The relay's tuning knobs, bound from <c>Sergin:Outbox</c>. Declared here, next to the storage it tunes,
/// rather than alongside the relay itself (a later task), so a host that only writes outbox rows — this task
/// — can bind and validate the section without a reference to relay code that does not exist yet.
/// </summary>
public sealed class OutboxOptions
{
    public const string SectionName = "Outbox";

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(5);

    public int BatchSize { get; set; } = 20;

    public int MaxAttempts { get; set; } = 10;

    public TimeSpan Retention { get; set; } = TimeSpan.FromDays(7);

    public TimeSpan PurgeInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Names the first key the relay could not run with, the way <c>DevUserOptions.Validate</c> does for
    /// <c>Sergin:DevUser</c>: a non-positive interval would spin the loop, a batch or attempt count below one
    /// would claim nothing or dead-letter everything on first sight.
    /// </summary>
    public bool Validate(out string failure)
    {
        if (PollInterval <= TimeSpan.Zero)
        {
            failure = $"Sergin:{SectionName}:{nameof(PollInterval)} must be a positive duration.";
            return false;
        }

        if (BatchSize < 1)
        {
            failure = $"Sergin:{SectionName}:{nameof(BatchSize)} must be at least 1.";
            return false;
        }

        if (MaxAttempts < 1)
        {
            failure = $"Sergin:{SectionName}:{nameof(MaxAttempts)} must be at least 1.";
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
