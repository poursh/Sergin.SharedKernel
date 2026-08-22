using Microsoft.Extensions.Options;

namespace Sergin.SharedKernel.Infrastructure.Dispatching;

/// <summary>
/// Fails startup naming exactly which module schema has no Sergin:Dispatch:Modules entry, rather than
/// letting an unlisted module silently fall through to a default. Constructed with a closure over the
/// registered modules' schemas by AddSerginBlazorApp, matching how SerginUiModuleCatalog is built there —
/// the collection isn't itself resolved from DI.
/// </summary>
/// <remarks>
/// Public, not internal: its only construction site, <c>AddSerginCore</c>, lives in
/// <c>Sergin.SharedKernel.Hosts</c> — the same assembly as this type's own
/// (<c>Sergin.SharedKernel.Infrastructure</c> is referenced by it), so this stays visible across that
/// boundary without an <c>InternalsVisibleTo</c>. Registration of <see cref="DispatchModeOptions"/>,
/// <c>ModuleDispatchRouteResolver</c>, and <c>ISerginSender</c>/<c>RoutingSerginSender</c> all live in
/// <c>AddSerginCore</c> itself now, so every Sergin host — Web API included, not just the Blazor UI host —
/// gets dispatch for free and must configure <c>Sergin:Dispatch:Modules</c> at startup, whether or not it
/// ever calls <c>ISerginSender</c>.
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
