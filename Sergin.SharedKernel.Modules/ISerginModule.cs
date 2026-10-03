using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sergin.SharedKernel.Modules;

public interface ISerginModule
{
    string Schema { get; }

    Assembly ApplicationAssembly { get; }

    Assembly ContractsAssembly { get; }

    /// <summary>
    /// The module's .Application.Configurations assembly: every command configuration and aggregate feature
    /// configuration it declares. The only assembly either registry is built from.
    /// </summary>
    Assembly ConfigurationsAssembly { get; }

    void AddServices(IServiceCollection services, IConfigurationSection configuration);

    Task MigrateAsync(IServiceProvider services);
}
