using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>
/// Refuses host start when the TickerQ licence file is not configured or does not exist, naming the key.
/// </summary>
internal sealed class TickerQLicenseGuard : IValidateOptions<TickerQScheduledTaskOptions>
{
    internal const string LicensePathKey = "Sergin:ScheduledTasks:TickerQ:LicensePath";

    public ValidateOptionsResult Validate(string? name, TickerQScheduledTaskOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.LicensePath))
        {
            return ValidateOptionsResult.Fail($"{LicensePathKey} is required.");
        }

        return File.Exists(options.LicensePath)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"{LicensePathKey} names a file that does not exist: '{options.LicensePath}'.");
    }
}
