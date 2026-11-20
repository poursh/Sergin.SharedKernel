namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What a task (<see cref="Types.ITaskCommand"/>) declared with <c>Task(...)</c>, or every default when it declared
/// nothing. <paramref name="Name"/> is the stable name its schedules and runs store, derived from its schema, root and
/// type (<c>dm.device.read-device-profile</c>) unless <c>Name(...)</c> kept an old one. <paramref name="InactivityTimeout"/>
/// is the longest silence one attempt may keep, <paramref name="MaxDuration"/> the hard cap on its wall time (null for
/// none), <paramref name="Cancelable"/> whether a person may cancel a run, and <paramref name="Schedulable"/> whether it may
/// be saved on a cron or as a run-once.
/// </summary>
public sealed record TaskSettings(string Name, TimeSpan InactivityTimeout, TimeSpan? MaxDuration, bool Cancelable, bool Schedulable)
{
    /// <summary>The inactivity timeout of a task that declares none.</summary>
    public static TimeSpan DefaultInactivityTimeout { get; } = TimeSpan.FromMinutes(5);

    /// <summary>The longest inactivity timeout a task may declare.</summary>
    public static TimeSpan MaxInactivityTimeout { get; } = TimeSpan.FromHours(1);
}
