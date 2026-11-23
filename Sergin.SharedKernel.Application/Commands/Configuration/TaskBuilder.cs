namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What a task's configuration declares inside <c>Task(...)</c>. Every member is optional: a task that calls none of
/// them, or has no configuration at all, gets every default. The registry refuses, at host start, an inactivity timeout
/// outside (0, <see cref="TaskSettings.MaxInactivityTimeout"/>] and a maximum duration that is not above zero, is below
/// the inactivity timeout or is above <see cref="TaskSettings.MaxMaxDuration"/>; the task catalogue refuses a malformed name, one outside the module's schema and one two
/// tasks share.
/// </summary>
public sealed class TaskBuilder
{
    internal TaskBuilder()
    {
    }

    internal TimeSpan? DeclaredInactivityTimeout { get; private set; }

    internal TimeSpan? DeclaredMaxDuration { get; private set; }

    internal bool IsNotCancelable { get; private set; }

    internal bool IsSchedulable { get; private set; }

    internal string? DeclaredName { get; private set; }

    /// <summary>
    /// The longest silence allowed: from the item's start to its first progress report, and between two reports.
    /// Defaults to <see cref="TaskSettings.DefaultInactivityTimeout"/>.
    /// </summary>
    public TaskBuilder InactivityTimeout(TimeSpan timeout)
    {
        DeclaredInactivityTimeout = timeout;
        return this;
    }

    /// <summary>A hard cap on one attempt's wall time, which nothing resets. None by default.</summary>
    public TaskBuilder MaxDuration(TimeSpan duration)
    {
        DeclaredMaxDuration = duration;
        return this;
    }

    /// <summary>A person may not cancel a run of this task: use it where a half-done run would harm the data.</summary>
    public TaskBuilder NotCancelable()
    {
        IsNotCancelable = true;
        return this;
    }

    /// <summary>The task may be saved on a cron or as a run-once at a time; without it, it can only be started now.</summary>
    public TaskBuilder Schedulable()
    {
        IsSchedulable = true;
        return this;
    }

    /// <summary>
    /// Overrides the derived name. Use it only to keep an old name after a rename, so the schedules that store it keep
    /// working. Calling it again replaces the name.
    /// </summary>
    public TaskBuilder Name(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        DeclaredName = name;
        return this;
    }
}
