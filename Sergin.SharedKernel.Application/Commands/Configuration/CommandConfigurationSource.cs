using System.Reflection;

namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// A set of configuration types to put in the registry, registered as a singleton per assembly the way
/// <c>AssemblyIntegrationEventSource</c> is. <c>AddSerginCore</c> adds one per module ConfigurationsAssembly; a test
/// host adds <see cref="FromTypes"/> for its own requests, so it never scans a test assembly whole.
/// </summary>
public sealed class CommandConfigurationSource
{
    private CommandConfigurationSource(IReadOnlyCollection<Type> configurationTypes)
    {
        ConfigurationTypes = configurationTypes;
    }

    public IReadOnlyCollection<Type> ConfigurationTypes { get; }

    public static CommandConfigurationSource FromAssembly(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        return new([.. assembly.GetTypes().Where(CommandConfigurationRegistry.IsConfigurationType)]);
    }

    public static CommandConfigurationSource FromTypes(params Type[] configurationTypes)
    {
        ArgumentNullException.ThrowIfNull(configurationTypes);

        return new([.. configurationTypes]);
    }
}
