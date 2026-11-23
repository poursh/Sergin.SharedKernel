using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>Where a run stands, as every list and panel of runs shows it.</summary>
public enum RunOutcome
{
    Running,
    Succeeded,
    PartlyFailed,
    Failed,
    Cancelled,
    Skipped,
}

/// <summary>The one rule that turns a run's status and item counts into its <see cref="RunOutcome"/>.</summary>
public static class RunOutcomes
{
    /// <param name="status">The run's stored status.</param>
    /// <param name="unfinished">Items still waiting or running.</param>
    /// <param name="succeeded">Items that succeeded.</param>
    /// <param name="failed">Items that failed.</param>
    /// <param name="cancelled">Items that were cancelled.</param>
    public static RunOutcome Of(TaskRunStatus status, int unfinished, int succeeded, int failed, int cancelled)
    {
        if (status == TaskRunStatus.Skipped)
        {
            return RunOutcome.Skipped;
        }

        if (unfinished > 0)
        {
            return RunOutcome.Running;
        }

        // A run cancelled part way through, with no failure, reads as cancelled: the finished message counts what was done.
        if (failed == 0)
        {
            return cancelled > 0 ? RunOutcome.Cancelled : RunOutcome.Succeeded;
        }

        return succeeded == 0 ? RunOutcome.Failed : RunOutcome.PartlyFailed;
    }
}
