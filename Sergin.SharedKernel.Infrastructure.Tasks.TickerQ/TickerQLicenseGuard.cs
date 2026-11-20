using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Infrastructure.Tasks.TickerQ;

/// <summary>
/// Refuses host start when the TickerQ licence file is not configured or does not exist, naming the key.
/// </summary>
internal sealed class TickerQLicenseGuard : IValidateOptions<TickerQTaskOptions>
{
    internal const string LicensePathKey = "Sergin:Tasks:TickerQ:LicensePath";

    public ValidateOptionsResult Validate(string? name, TickerQTaskOptions options)
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
