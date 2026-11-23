using Microsoft.AspNetCore.Components;

namespace Sergin.SharedKernel.Presentation.Blazor.Tasks.Runs;

/// <summary>The Task runs page: hosts <see cref="SerginTaskRunsView"/>, which owns the content.</summary>
public sealed partial class SerginTaskRunsPage
{
    /// <summary>The page's route, which the shell's nav entry links to.</summary>
    public const string Route = "/sys/tasks/runs";

    /// <summary>The schema of the module whose runs are shown; the first in drawer order when absent or unknown.</summary>
    [SupplyParameterFromQuery(Name = "module")]
    public string? Module { get; set; }
}
