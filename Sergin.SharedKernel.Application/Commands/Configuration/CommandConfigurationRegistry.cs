using System.Reflection;

namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What every <see cref="ICommandConfiguration{TCommand}"/> declared, keyed by request type. Built once per
/// host from every registered <see cref="CommandConfigurationSource"/> and read by the permission and
/// expected-version pipeline behaviors on each send. Refuses two configurations for one request, a
/// configuration without a parameterless constructor, an expected version on a request that is not an
/// <see cref="ICommand{TResponse}"/>, and a configuration whose Configure throws — each naming the type.
/// </summary>
public sealed class CommandConfigurationRegistry
{
    private readonly IReadOnlyDictionary<Type, CommandSettings> settings;

    private CommandConfigurationRegistry(IReadOnlyDictionary<Type, CommandSettings> settings)
    {
        this.settings = settings;
    }

    public static CommandConfigurationRegistry Empty { get; } = new(new Dictionary<Type, CommandSettings>());

    /// <summary>The configured request types.</summary>
    public IReadOnlyCollection<Type> ConfiguredTypes => [.. settings.Keys];

    /// <summary>The settings <paramref name="requestType"/> declared; None for an unconfigured request.</summary>
    public CommandSettings For(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        return settings.TryGetValue(requestType, out CommandSettings? declared) ? declared : CommandSettings.None;
    }

    public static CommandConfigurationRegistry FromSources(IEnumerable<CommandConfigurationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        return FromConfigurationTypes(sources.SelectMany(source => source.ConfigurationTypes).Distinct());
    }

    public static CommandConfigurationRegistry FromConfigurationTypes(IEnumerable<Type> configurationTypes)
    {
        ArgumentNullException.ThrowIfNull(configurationTypes);

        List<(Type RequestType, Type ConfigurationType, CommandSettings Settings)> declared = [];

        foreach (Type configurationType in configurationTypes)
        {
            foreach (Type closedInterface in configurationType.GetInterfaces().Where(IsClosedConfigurationInterface))
            {
                Type requestType = closedInterface.GetGenericArguments()[0];
                declared.Add((requestType, configurationType, Run(configurationType, closedInterface, requestType)));
            }
        }

        string[] duplicates =
        [
            .. declared
                .GroupBy(item => item.RequestType)
                .Where(group => group.Count() > 1)
                .Select(group => $"{group.Key.FullName} ({string.Join(", ", group.Select(item => item.ConfigurationType.FullName))})")
        ];

        if (duplicates.Length > 0)
        {
            throw new InvalidOperationException(
                $"More than one command configuration is declared for: {string.Join("; ", duplicates)}. "
                + "Declare each request's policy in exactly one ICommandConfiguration<T>.");
        }

        return new CommandConfigurationRegistry(declared.ToDictionary(item => item.RequestType, item => item.Settings));
    }

    internal static bool IsConfigurationType(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(IsClosedConfigurationInterface);

    private static CommandSettings Run(Type configurationType, Type closedInterface, Type requestType)
    {
        if (requestType.IsAbstract || requestType.IsInterface)
        {
            throw new InvalidOperationException(
                $"Command configuration {configurationType.FullName} configures {requestType.FullName}, which is abstract or an interface. "
                + "Settings are looked up by the exact type sent, so they would never apply: configure each concrete request.");
        }

        object configuration;

        try
        {
            configuration = Activator.CreateInstance(configurationType, nonPublic: true)!;
        }
        catch (MissingMethodException exception)
        {
            throw new InvalidOperationException(
                $"Command configuration {configurationType.FullName} must have a parameterless constructor: "
                + "command configurations are declarations, created without dependency injection.",
                exception);
        }

        var builder = (CommandConfigurationBuilder)Activator.CreateInstance(
            typeof(CommandConfigurationBuilder<>).MakeGenericType(requestType), nonPublic: true)!;

        try
        {
            closedInterface
                .GetMethod(nameof(ICommandConfiguration<>.Configure))!
                .Invoke(configuration, BindingFlags.DoNotWrapExceptions, binder: null, [builder], culture: null);
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Command configuration {configurationType.FullName} failed while configuring {requestType.FullName}: {exception.Message}",
                exception);
        }

        CommandSettings declared = builder.Settings;

        if (declared.RequiresExpectedVersion && !IsCommand(requestType))
        {
            throw new InvalidOperationException(
                $"Command configuration {configurationType.FullName} requires an expected version for {requestType.FullName}, "
                + "which is not an ICommand<T>. Only a command writes, so only a command can carry an expected version.");
        }

        return declared;
    }

    private static bool IsCommand(Type requestType) =>
        requestType.GetInterfaces().Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommand<>));

    private static bool IsClosedConfigurationInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommandConfiguration<>);
}
