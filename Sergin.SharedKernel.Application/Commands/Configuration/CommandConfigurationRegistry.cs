using System.Reflection;
using Sergin.SharedKernel.Application.Aggregates;

namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What every <see cref="ICommandConfiguration{TCommand}"/> declared, keyed by request type. Built once per
/// host from every registered <see cref="CommandConfigurationSource"/> and read by the permission and
/// expected-version pipeline behaviors on each send. Refuses two configurations for one request, a
/// configuration without a parameterless constructor, an expected version on a request that is not an
/// <see cref="ICommand{TResponse}"/> or whose aggregate is not versioned, an explicit scope with no permission to
/// check, and a configuration whose Configure throws — each naming the type. A request bound to an aggregate
/// through <see cref="ICommandConfiguration{TCommand, TAggregateRoot}"/> is checked on the root scope unless that
/// aggregate is scoped, read from the aggregate feature configurations the same sources carry.
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

    /// <summary>
    /// Every permission any configuration requires, plus <see cref="Permission.AllPlatform"/>, ordered by name —
    /// the catalogue a role editor offers. Permissions are declared by developers in configurations, never created
    /// at runtime, so this is the complete set the platform checks.
    /// </summary>
    public IReadOnlyCollection<Permission> KnownPermissions =>
    [
        .. settings.Values
            .SelectMany(declared => declared.RequiredPermissions)
            .Append(Permission.AllPlatform)
            .DistinctBy(permission => permission.Value)
            .OrderBy(permission => permission.Value, StringComparer.Ordinal)
    ];

    /// <summary>The settings <paramref name="requestType"/> declared; None for an unconfigured request.</summary>
    public CommandSettings For(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        return settings.TryGetValue(requestType, out CommandSettings? declared) ? declared : CommandSettings.None;
    }

    public static CommandConfigurationRegistry FromSources(IEnumerable<CommandConfigurationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        CommandConfigurationSource[] all = [.. sources];

        return Build(
            all.SelectMany(source => source.ConfigurationTypes).Distinct(),
            AggregateFeatureRegistry.FromConfigurationTypes(all.SelectMany(source => source.AggregateConfigurationTypes).Distinct()));
    }

    /// <summary>Command and aggregate feature configuration types, in any mix.</summary>
    public static CommandConfigurationRegistry FromConfigurationTypes(IEnumerable<Type> configurationTypes)
    {
        ArgumentNullException.ThrowIfNull(configurationTypes);

        return FromSources([CommandConfigurationSource.FromTypes([.. configurationTypes])]);
    }

    private static CommandConfigurationRegistry Build(IEnumerable<Type> configurationTypes, AggregateFeatureRegistry aggregates)
    {
        List<(Type RequestType, Type ConfigurationType, CommandSettings Settings)> declared = [];

        foreach (Type configurationType in configurationTypes)
        {
            foreach (Type closedInterface in configurationType.GetInterfaces().Where(IsClosedConfigurationInterface))
            {
                Type[] arguments = closedInterface.GetGenericArguments();
                Type requestType = arguments[0];
                Type? aggregateRoot = arguments.Length > 1 ? arguments[1] : null;
                declared.Add((requestType, configurationType, Run(configurationType, closedInterface, requestType, aggregateRoot, aggregates)));
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

    private static CommandSettings Run(
        Type configurationType,
        Type closedInterface,
        Type requestType,
        Type? aggregateRoot,
        AggregateFeatureRegistry aggregates)
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

        AggregateFeatures features = aggregateRoot is null ? AggregateFeatures.None : aggregates.For(aggregateRoot);

        if (builder.ExpectedVersionRequired && !IsCommand(requestType))
        {
            throw new InvalidOperationException(
                $"Command configuration {configurationType.FullName} requires an expected version for {requestType.FullName}, "
                + "which is not an ICommand<T>. Only a command writes, so only a command can carry an expected version.");
        }

        if (builder.ExpectedVersionRequired && aggregateRoot is not null && !features.Versioned)
        {
            throw new InvalidOperationException(
                $"Command configuration {configurationType.FullName} requires an expected version for {requestType.FullName}, "
                + $"but binds it to {aggregateRoot.FullName}, which is not Versioned(): there is no version to check. "
                + "Call Versioned() in its aggregate feature configuration, or drop RequireExpectedVersion().");
        }

        if (builder.CheckedAtRoot is not null && builder.Permissions.Count == 0)
        {
            string call = builder.CheckedAtRoot.Value ? "AtRoot()" : "InCurrentScope()";

            throw new InvalidOperationException(
                $"Command configuration {configurationType.FullName} chooses the scope {requestType.FullName} is checked on "
                + $"but requires no permission. Call RequirePermissions(...) with {call}, or drop {call}.");
        }

        // Unbound: the current scope. Bound: where the aggregate's records live, the root unless it is scoped.
        bool atRoot = builder.CheckedAtRoot ?? (aggregateRoot is not null && !features.Scoped);

        return builder.Permissions.Count == 0 && !builder.ExpectedVersionRequired && aggregateRoot is null
            ? CommandSettings.None
            : new CommandSettings([.. builder.Permissions.Distinct()], builder.ExpectedVersionRequired, atRoot, aggregateRoot);
    }

    private static bool IsCommand(Type requestType) =>
        requestType.GetInterfaces().Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommand<>));

    private static bool IsClosedConfigurationInterface(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(ICommandConfiguration<>)
            || type.GetGenericTypeDefinition() == typeof(ICommandConfiguration<,>));
}
