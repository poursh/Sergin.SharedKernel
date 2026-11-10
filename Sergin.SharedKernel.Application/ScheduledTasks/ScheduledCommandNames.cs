using System.Text.RegularExpressions;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>The shape of a schedulable command's stable name: <c>dm.devices.read-profile</c>.</summary>
public static partial class ScheduledCommandNames
{
    public static bool IsValid(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return Pattern().IsMatch(name);
    }

    [GeneratedRegex(@"^[a-z][a-z0-9]*(\.[a-z][a-z0-9]*(-[a-z0-9]+)*)+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Pattern();
}
