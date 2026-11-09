using System.Reflection;
using Sergin.SharedKernel.Application.Commands;

namespace Sergin.SharedKernel.Hosts.Commands;

/// <summary>
/// The Contracts assemblies of the modules this host runs in process (AddSerginCore's local modules), without the
/// remote ones. The command-target mapping guard reads only these: a remote module's records are mapped by a
/// DbContext in another process, which a gateway does not have.
/// </summary>
public sealed record LocalContractsAssemblies(IReadOnlyCollection<Assembly> Assemblies)
{
    /// <summary>The concrete requests (<see cref="IBaseCommand"/>) the assemblies hold.</summary>
    public IReadOnlyCollection<Type> RequestTypes() =>
    [
        .. Assemblies.Distinct().SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
                && typeof(IBaseCommand).IsAssignableFrom(type)),
    ];
}
