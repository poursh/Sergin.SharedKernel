using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Hosts.Dispatching;

/// <summary>
/// Fails startup naming exactly which module schema has no Sergin:Dispatch:Modules entry, rather than
/// letting an unlisted module silently fall through to a default. Constructed with a closure over the
/// registered modules' schemas by AddSerginCore, matching how SerginUiModuleCatalog is built in
/// AddSerginBlazorApp — the collection isn't itself resolved from DI.
/// </summary>
internal sealed class DispatchModeOptionsValidator(IReadOnlyCollection<string> requiredSchemas)
    : IValidateOptions<DispatchModeOptions>
{
    public ValidateOptionsResult Validate(string? name, DispatchModeOptions options)
    {
        string[] missing = [.. requiredSchemas.Where(schema => !options.Modules.ContainsKey(schema))];

        return missing.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Sergin:{SerginCoreExtensions.SectionName}:{DispatchModeOptions.SectionName}:Modules is missing "
                + $"an entry for: {string.Join(", ", missing)}.");
    }
}
