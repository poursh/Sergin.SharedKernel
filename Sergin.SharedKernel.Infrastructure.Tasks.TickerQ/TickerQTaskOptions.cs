namespace Sergin.SharedKernel.Infrastructure.Tasks.TickerQ;

/// <summary>
/// The TickerQ engine settings, bound from <c>Sergin:Tasks:TickerQ</c>.
/// </summary>
public sealed class TickerQTaskOptions
{
    public const string SectionName = "Tasks:TickerQ";

    /// <summary>The TickerQ licence file. Never committed; supplied by configuration or user secrets.</summary>
    public string LicensePath { get; set; } = string.Empty;
}
