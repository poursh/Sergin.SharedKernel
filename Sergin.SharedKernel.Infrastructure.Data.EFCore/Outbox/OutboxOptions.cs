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
}
