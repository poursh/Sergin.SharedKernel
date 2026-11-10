using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

public static class ScheduledTaskOptionsBuilderExtensions
{
    /// <summary>
    /// Applies <see cref="ScheduledTaskOptions.DefaultRetryIntervalsSeconds"/> after binding when no interval was
    /// configured, so a configured list replaces the default instead of being appended to it.
    /// </summary>
    public static OptionsBuilder<ScheduledTaskOptions> WithDefaults(this OptionsBuilder<ScheduledTaskOptions> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.PostConfigure(options =>
        {
            if (options.RetryIntervalsSeconds is not { Count: > 0 })
            {
                options.RetryIntervalsSeconds = ScheduledTaskOptions.DefaultRetryIntervalsSeconds;
            }
        });
    }
}
