namespace Sergin.SharedKernel.Hosts.Dispatching;

public sealed class DispatchModeOptions
{
    public const string SectionName = "Dispatch";

    public Dictionary<string, DispatchMode> Modules { get; set; } = [];
}

public enum DispatchMode
{
    Local,
    Remote,
}
