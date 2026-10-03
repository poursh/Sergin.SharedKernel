using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sergin.SharedKernel.Modules;

/// <summary>
/// A module a host calls Remote — no real handlers, no DbContext, nothing to migrate. Deliberately not
/// ISerginModule: that contract's ApplicationAssembly/MigrateAsync assume the module runs locally. The
/// type implementing this must not be the module's composition root (ISerginModule implementer) if that
/// root transitively references the module's .Application/.Infrastructure — doing so would force a
/// gateway host to pull in everything just to reach this capability, defeating the point. See
/// DeviceManagementRemoteModule (host repo) for the reference shape: a small class living inside the
/// module's own .Presentation.Grpc project, which is already isolated from .Application/.Infrastructure.
/// </summary>
public interface ISerginRemoteModule
{
    string Schema { get; }

    Assembly ContractsAssembly { get; }

    /// <summary>
    /// The module's .Application.Configurations assembly. A gateway builds its command configuration registry
    /// from it, so a forbidden remote call is refused before the gRPC hop.
    /// </summary>
    Assembly ConfigurationsAssembly { get; }

    void AddRemoteServices(IServiceCollection services, IConfigurationSection configuration);
}
