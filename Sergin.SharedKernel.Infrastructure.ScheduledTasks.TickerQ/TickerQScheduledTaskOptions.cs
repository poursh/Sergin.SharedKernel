namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>
/// The TickerQ engine settings, bound from <c>Sergin:ScheduledTasks:TickerQ</c>.
/// </summary>
public sealed class TickerQScheduledTaskOptions
{
    public const string SectionName = "ScheduledTasks:TickerQ";

    /// <summary>The TickerQ licence file. Never committed; supplied by configuration or user secrets.</summary>
    public string LicensePath { get; set; } = string.Empty;
}
