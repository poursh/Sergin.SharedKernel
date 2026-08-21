using System.Reflection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Hosts.Dispatching;

namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

/// <summary>
/// Maps a request type to its owning module's schema via the request's declaring assembly (the same
/// reflection style UseSerginWebUiAsync's @page prefix guard already uses), then looks that schema up
/// in DispatchModeOptions. Constructed with a closure over the registered modules by whichever host
/// bootstrap calls AddSerginBlazorApp — not resolved from DI, matching SerginUiModuleCatalog's and
/// DispatchModeOptionsValidator's precedent.
/// </summary>
/// <remarks>
/// Public, not internal: it is constructed directly (<c>new ModuleDispatchRouteResolver(...)</c>) from
/// <c>Sergin.SharedKernel.Hosts.WebUi</c>, a separate assembly — the same cross-assembly-construction
/// situation <see cref="Modules.SerginUiModuleCatalog"/> is in, resolved the same way there and per the
/// same reasoning documented on <c>SerginHomeBuilder.Build()</c>: an <c>InternalsVisibleTo</c> for one
/// call site costs more than the encapsulation it buys.
/// </remarks>
public sealed class ModuleDispatchRouteResolver(
    IReadOnlyDictionary<Assembly, string> schemaByAssembly,
    IOptions<DispatchModeOptions> options) : IDispatchRouteResolver
{
    public bool IsRemote(Type requestType)
    {
        Type schemaSourceType = ResolveSchemaSourceType(requestType);

        if (!schemaByAssembly.TryGetValue(schemaSourceType.Assembly, out string? schema))
        {
            throw new InvalidOperationException(
                $"'{requestType.FullName}' does not belong to any registered module's ApplicationAssembly.");
        }

        if (!options.Value.Modules.TryGetValue(schema, out DispatchMode mode))
        {
            throw new InvalidOperationException($"No dispatch mode configured for module schema '{schema}'.");
        }

        return mode == DispatchMode.Remote;
    }

    /// <summary>
    /// List queries have no module-specific request type (see the "CQRS structural gotchas" note in the
    /// host repo's CLAUDE.md): SendListAsync always builds a closed ListQuery&lt;TResponseData&gt; or
    /// ListQuery&lt;TRequestData, TResponseData&gt;, and both generic type definitions live in
    /// Sergin.SharedKernel.Application itself, not in any module's ApplicationAssembly. Type.Assembly on
    /// a closed generic type returns where the *generic type definition* is declared, so
    /// requestType.Assembly for ListQuery&lt;GetUserListItem&gt; is always this SharedKernel assembly,
    /// never Sergin.UserAccess.Application — which would make every list-query dispatch throw below,
    /// regardless of configured DispatchMode. Unwrap to the last type argument (the response-item type,
    /// e.g. GetUserListItem) instead, which does belong to a module's ApplicationAssembly.
    /// </summary>
    private static Type ResolveSchemaSourceType(Type requestType)
    {
        if (!requestType.IsGenericType)
        {
            return requestType;
        }

        Type definition = requestType.GetGenericTypeDefinition();

        return definition == typeof(ListQuery<>) || definition == typeof(ListQuery<,>)
            ? requestType.GetGenericArguments()[^1]
            : requestType;
    }
}
