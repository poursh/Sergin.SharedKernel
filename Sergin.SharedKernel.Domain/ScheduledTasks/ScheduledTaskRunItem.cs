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

    public static ScheduledTaskRunItem Create(Guid runId, Guid targetId, Guid? rootId) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            RunId = runId,
            TargetId = targetId,
            RootId = rootId,
            Status = ScheduledRunItemStatus.Pending,
        };

    public void LinkJob(Guid engineJobId) => EngineJobId = engineJobId;

    /// <summary>An attempt starts: counted, and the first one's time kept.</summary>
    public void MarkRunning(DateTime at)
    {
        Status = ScheduledRunItemStatus.Running;
        Attempts++;
        StartedAtUtc ??= at;
    }

    public void MarkSucceeded(DateTime at)
    {
        Status = ScheduledRunItemStatus.Succeeded;
        ErrorCode = null;
        ErrorDescription = null;
        CompletedAtUtc = at;
    }

    public void MarkRetrying(string code, string description)
    {
        Status = ScheduledRunItemStatus.Retrying;
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
