using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Infrastructure.Dispatching;

/// <summary>
/// Fails startup naming exactly which module schema has no Sergin:Dispatch:Modules entry, rather than
/// letting an unlisted module silently fall through to a default. Constructed with a closure over the
/// registered modules' schemas by AddSerginBlazorApp, matching how SerginUiModuleCatalog is built there —
/// the collection isn't itself resolved from DI.
/// </summary>
/// <remarks>
/// Public, not internal: its only construction site, <c>AddSerginBlazorApp</c>, lives in
/// <c>Sergin.SharedKernel.Hosts.WebUi</c> — a separate assembly from this type's own
/// (<c>Sergin.SharedKernel.Hosts</c>) — and an <c>InternalsVisibleTo</c> for one call site costs more than
/// the encapsulation it buys (same reasoning as <c>SerginHomeBuilder.Build()</c>). The registration calls
/// that construct it moved here from <c>AddSerginCore</c>, which no longer references
/// <see cref="DispatchModeOptions"/> at all: a future Web API host that also calls <c>AddSerginCore</c> has
/// no <c>ISerginUiDispatcher</c> and no route resolver, so it must not be forced to configure
/// <c>Sergin:Dispatch:Modules</c> at startup.
/// </remarks>
public sealed class DispatchModeOptionsValidator(IReadOnlyCollection<string> requiredSchemas)
    : IValidateOptions<DispatchModeOptions>
{
    public ValidateOptionsResult Validate(string? name, DispatchModeOptions options)
    {
        string[] missing = [.. requiredSchemas.Where(schema => !options.Modules.ContainsKey(schema))];

        return missing.Length == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Sergin:{DispatchModeOptions.SectionName}:Modules is missing "
                + $"an entry for: {string.Join(", ", missing)}.");
    }
}
