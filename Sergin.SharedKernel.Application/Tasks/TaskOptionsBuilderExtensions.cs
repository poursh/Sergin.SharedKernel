using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Application.Tasks;

public static class TaskOptionsBuilderExtensions
{
    /// <summary>
    /// Applies <see cref="TaskOptions.DefaultRetryIntervalsSeconds"/> after binding when no interval was
    /// configured, so a configured list replaces the default instead of being appended to it.
    /// </summary>
    public static OptionsBuilder<TaskOptions> WithDefaults(this OptionsBuilder<TaskOptions> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.PostConfigure(options =>
        {
            if (options.RetryIntervalsSeconds is not { Count: > 0 })
            {
                options.RetryIntervalsSeconds = TaskOptions.DefaultRetryIntervalsSeconds;
            }
        });
    }
}
