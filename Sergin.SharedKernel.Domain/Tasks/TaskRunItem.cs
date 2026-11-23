namespace Sergin.SharedKernel.Domain.Tasks;

/// <summary>One target of one run: its own status, attempts and last error, so one target's failure fails only itself.</summary>
public sealed class TaskRunItem
{
    private TaskRunItem()
    {
    }

    public Guid Id { get; private set; }

    public Guid RunId { get; private set; }

    /// <summary>The record the item works on; null for the one item of a collection task's run.</summary>
    public Guid? TargetId { get; private set; }

    /// <summary>The target's title as the person saw it when the run started, as stored (user data); null when none was given.</summary>
    public string? TargetLabel { get; private set; }

    public TaskRunItemStatus Status { get; private set; }

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

    /// <summary>
    /// How far the command said it got, 0 to 100; null until it reports. Each attempt starts again from null, and a
    /// finished item keeps the last value its command reported, whatever the outcome.
    /// </summary>
    public int? Progress { get; private set; }

    /// <summary>The highest progress, in percent.</summary>
    public const int MaxProgress = 100;

    /// <summary>The longest label kept; a longer one is cut at start.</summary>
    public const int MaxTargetLabelLength = 200;

    public static TaskRunItem Create(Guid runId, Guid? targetId, DateTime at, string? targetLabel = null) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            RunId = runId,
            TargetId = targetId,
            TargetLabel = targetLabel,
            Status = TaskRunItemStatus.Pending,
            LastActivityAtUtc = at,
        };

    public void LinkJob(Guid engineJobId, DateTime at)
    {
        EngineJobId = engineJobId;
        LastActivityAtUtc = at;
    }

    /// <summary>
    /// An attempt starts: counted, the first one's time kept, and the last attempt's progress cleared. The executor claims an item in the database instead
    /// (the same change, made by one conditional update so two deliveries cannot both start it); this is its in-memory form.
    /// </summary>
    public void MarkRunning(DateTime at)
    {
        Status = TaskRunItemStatus.Running;
        Attempts++;
        StartedAtUtc ??= at;
        LastActivityAtUtc = at;
        Progress = null;
    }

    /// <summary>The command's progress, clamped to 0–100.</summary>
    public void SetProgress(int percent) => Progress = Math.Clamp(percent, 0, MaxProgress);

    public void MarkSucceeded(DateTime at)
    {
        Status = TaskRunItemStatus.Succeeded;
        ErrorCode = null;
        ErrorDescription = null;
        CompletedAtUtc = at;
    }

    public void MarkRetrying(string code, string description, DateTime at)
    {
        Status = TaskRunItemStatus.Retrying;
        LastActivityAtUtc = at;
        ErrorCode = code;
        ErrorDescription = description;
    }

    public void MarkFailed(string code, string description, DateTime at)
    {
        Status = TaskRunItemStatus.Failed;
        ErrorCode = code;
        ErrorDescription = description;
        CompletedAtUtc = at;
    }

    /// <summary>
    /// The run was cancelled before this item finished: final, never retried. Its last error, if a retry left one, is kept.
    /// The store cancels items in the database instead (one update under the run's lock); this is its in-memory form.
    /// </summary>
    public void MarkCancelled(DateTime at)
    {
        Status = TaskRunItemStatus.Cancelled;
        CompletedAtUtc = at;
    }
}

public enum TaskRunItemStatus
{
    Pending,
    Running,
    Retrying,
    Succeeded,
    Failed,

    /// <summary>The run was cancelled before the item finished. Final, like Succeeded and Failed.</summary>
    Cancelled,
}
