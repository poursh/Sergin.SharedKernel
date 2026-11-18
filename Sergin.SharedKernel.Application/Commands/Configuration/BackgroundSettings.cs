namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What a configuration declared with <c>Background(timeout)</c>: the command runs only as a background run item.
/// <paramref name="Timeout"/> is how long one run item may take before it is stopped, or null for the platform default.
/// </summary>
public sealed record BackgroundSettings(TimeSpan? Timeout)
{
    /// <summary>The longest timeout a command may declare.</summary>
    public static TimeSpan MaxTimeout { get; } = TimeSpan.FromHours(1);
}
