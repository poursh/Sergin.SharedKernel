using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Sergin.SharedKernel.Modules;

public interface ISerginModule
{
    string Schema { get; }

    Assembly ApplicationAssembly { get; }

    void AddServices(IServiceCollection services, IConfigurationSection configuration);

    Task MigrateAsync(IServiceProvider services);
}
