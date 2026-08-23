using System.Reflection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Commands.Queries;
using Sergin.SharedKernel.Application.Dispatching;

namespace Sergin.SharedKernel.Infrastructure.Dispatching;

/// <summary>
/// Maps a request type to its owning module's schema via the request's declaring assembly, then looks
/// that schema up in DispatchModeOptions. Constructed with a closure over the registered modules by
/// AddSerginCore (Sergin.SharedKernel.Hosts) — not resolved from DI, matching SerginUiModuleCatalog's
/// and DispatchModeOptionsValidator's precedent.
/// </summary>
internal sealed class ModuleDispatchRouteResolver(
    IReadOnlyDictionary<Assembly, string> schemaByAssembly,
    IOptions<DispatchModeOptions> options) : IDispatchRouteResolver
{
    public bool IsRemote(Type requestType)
    {
        Type schemaSourceType = ResolveSchemaSourceType(requestType);

        if (!schemaByAssembly.TryGetValue(schemaSourceType.Assembly, out string? schema))
        {
            throw new InvalidOperationException(
                $"'{requestType.FullName}' does not belong to any registered module's ApplicationAssembly "
                + "or ContractsAssembly.");
        }

        if (!options.Value.Modules.TryGetValue(schema, out DispatchMode mode))
        {
            throw new InvalidOperationException($"No dispatch mode configured for module schema '{schema}'.");
        }

        return mode == DispatchMode.Remote;
    }

    /// <summary>
    /// List queries have no module-specific request type: SendListAsync always builds a closed
    /// ListQuery&lt;TResponseData&gt; or ListQuery&lt;TRequestData, TResponseData&gt;, and both generic type
    /// definitions live in Sergin.SharedKernel.Application itself, not in any module's ApplicationAssembly or
    /// ContractsAssembly. Type.Assembly on a closed generic type returns where the *generic type definition*
    /// is declared, so requestType.Assembly for ListQuery&lt;GetUserListItem&gt; is always this SharedKernel
    /// assembly, never a module's — which would make every list-query dispatch throw above, regardless of
    /// configured DispatchMode. Unwrap to the last type argument (the response-item type, e.g.
    /// GetUserListItem) instead, which does belong to a module's ApplicationAssembly or ContractsAssembly.
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
