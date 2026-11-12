namespace Sergin.SharedKernel.Domain.ScheduledTasks;

/// <summary>One target of one run: its own status, attempts and last error, so one target's failure fails only itself.</summary>
public sealed class ScheduledTaskRunItem
{
    private ScheduledTaskRunItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    public Guid TargetId { get; private set; }

    public Guid? RootId { get; private set; }

    public ScheduledRunItemStatus Status { get; private set; }

    public int Attempts { get; private set; }

    public string? ErrorCode { get; private set; }

    public string? ErrorDescription { get; private set; }

    public DateTime? StartedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    /// <summary>The engine's job for this item; null until linked.</summary>
    public Guid? EngineJobId { get; private set; }

    /// <summary>
    /// When anything last happened to the item: created, handed to the engine, an attempt started, a retry asked for.
    /// An unfinished item quiet for longer than the recovery's stale period lost its job and is handed over again.
    /// </summary>
    public DateTime LastActivityAtUtc { get; private set; }

    public static ScheduledTaskRunItem Create(Guid runId, Guid targetId, Guid? rootId, DateTime at) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            RunId = runId,
            TargetId = targetId,
            RootId = rootId,
            Status = ScheduledRunItemStatus.Pending,
            LastActivityAtUtc = at,
        };

    public void LinkJob(Guid engineJobId, DateTime at)
    {
        EngineJobId = engineJobId;
        LastActivityAtUtc = at;
    }

    /// <summary>
    /// An attempt starts: counted, and the first one's time kept. The executor claims an item in the database instead
    /// (the same change, made by one conditional update so two deliveries cannot both start it); this is its in-memory form.
    /// </summary>
    public void MarkRunning(DateTime at)
    {
        Status = ScheduledRunItemStatus.Running;
        Attempts++;
        StartedAtUtc ??= at;
        LastActivityAtUtc = at;
    }

    public void MarkSucceeded(DateTime at)
    {
        Status = ScheduledRunItemStatus.Succeeded;
        ErrorCode = null;
        ErrorDescription = null;
        CompletedAtUtc = at;
    }

    public void MarkRetrying(string code, string description, DateTime at)
    {
        Status = ScheduledRunItemStatus.Retrying;
        LastActivityAtUtc = at;
        ErrorCode = code;
        ErrorDescription = description;
    }

    public void MarkFailed(string code, string description, DateTime at)
    {
        Status = ScheduledRunItemStatus.Failed;
        ErrorCode = code;
        ErrorDescription = description;
        CompletedAtUtc = at;
    }
}

public enum ScheduledRunItemStatus
{
    Pending,
    Running,
    Retrying,
    Succeeded,
    Failed,
}
