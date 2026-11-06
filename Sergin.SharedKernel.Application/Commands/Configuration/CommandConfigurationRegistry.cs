using System.Reflection;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Localizations;

namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What every <see cref="ICommandConfiguration{TCommand}"/> declared, keyed by request type. Built once per
/// host from every registered <see cref="ModuleConfigurationSource"/> and read by the permission and
/// expected-version pipeline behaviors on each send. Refuses two configurations for one request, a
/// configuration without a parameterless constructor, an expected version on a request that is not an
/// <see cref="ICommand{TResponse}"/> or whose aggregate is not versioned, and a configuration whose Configure
/// throws, and a required permission no aggregate registered — each naming the type. A request bound to an
/// aggregate through <see cref="ICommandConfiguration{TCommand, TAggregateRoot}"/> is checked on any scope unless
/// that aggregate is scoped, read from the aggregate feature configurations the same sources carry.
/// </summary>
public sealed class CommandConfigurationRegistry
{
    private static readonly IReadOnlySet<string> noProperties = new HashSet<string>(StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<Type, CommandSettings> settings;
    private readonly IReadOnlyDictionary<Type, Texts> texts;

    private CommandConfigurationRegistry(IReadOnlyDictionary<Type, CommandSettings> settings, IReadOnlyDictionary<Type, Texts> texts)
    {
        this.settings = settings;
        this.texts = texts;
    }

    public static CommandConfigurationRegistry Empty { get; } =
        new(new Dictionary<Type, CommandSettings>(), new Dictionary<Type, Texts>());

    /// <summary>The configured request types.</summary>
    public IReadOnlyCollection<Type> ConfiguredTypes => [.. settings.Keys];

    /// <summary>The settings <paramref name="requestType"/> declared; None for an unconfigured request.</summary>
    public CommandSettings For(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        return settings.TryGetValue(requestType, out CommandSettings? declared) ? declared : CommandSettings.None;
    }

    /// <summary>
    /// The text category <paramref name="requestType"/>'s fields are declared in: the one its configuration names with
    /// <c>Texts(...)</c>, else its bound root's; null for a request with neither, or with no configuration.
    /// </summary>
    public TextCategory? CategoryOf(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        return texts.TryGetValue(requestType, out Texts? declared) ? declared.Category : null;
    }

    /// <summary>The property names <paramref name="requestType"/>'s configuration marks <c>NotShown(...)</c>; empty when none.</summary>
    public IReadOnlySet<string> HiddenProperties(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        return texts.TryGetValue(requestType, out Texts? declared) ? declared.HiddenProperties : noProperties;
    }

    public static CommandConfigurationRegistry FromSources(IEnumerable<ModuleConfigurationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        ModuleConfigurationSource[] all = [.. sources];

        return Build(
            all.SelectMany(source => source.ConfigurationTypes).Distinct(),
            AggregateFeatureRegistry.FromConfigurationTypes(all.SelectMany(source => source.AggregateConfigurationTypes).Distinct()));
    }

    /// <summary>Command and aggregate feature configuration types, in any mix.</summary>
    public static CommandConfigurationRegistry FromConfigurationTypes(IEnumerable<Type> configurationTypes)
    {
        ArgumentNullException.ThrowIfNull(configurationTypes);

        return FromSources([ModuleConfigurationSource.FromTypes([.. configurationTypes])]);
    }

    private static CommandConfigurationRegistry Build(IEnumerable<Type> configurationTypes, AggregateFeatureRegistry aggregates)
    {
        List<(Type RequestType, Type ConfigurationType, CommandSettings Settings, Texts Texts)> declared = [];

        foreach (Type configurationType in configurationTypes)
        {
            foreach (Type closedInterface in configurationType.GetInterfaces().Where(IsClosedConfigurationInterface))
            {
                Type[] arguments = closedInterface.GetGenericArguments();
                Type requestType = arguments[0];
                Type? aggregateRoot = arguments.Length > 1 ? arguments[1] : null;
                (CommandSettings requestSettings, Texts requestTexts) =
                    Run(configurationType, closedInterface, requestType, aggregateRoot, aggregates);
                declared.Add((requestType, configurationType, requestSettings, requestTexts));
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

        return new CommandConfigurationRegistry(
            declared.ToDictionary(item => item.RequestType, item => item.Settings),
            declared.ToDictionary(item => item.RequestType, item => item.Texts));
    }

    internal static bool IsConfigurationType(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(IsClosedConfigurationInterface);

    private static (CommandSettings Settings, Texts Texts) Run(
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

        EnsureRegistered(configurationType, requestType, builder.Permissions, aggregates);

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

        // Unbound: the current scope. Bound: the current scope for a scoped aggregate, whose records live in
        // scopes; any scope for another, whose records belong to none.
        PermissionScope scope = aggregateRoot is not null && !features.Scoped ? PermissionScope.Any : PermissionScope.Current;

        CommandSettings declared = builder.Permissions.Count == 0 && !builder.ExpectedVersionRequired && aggregateRoot is null
            ? CommandSettings.None
            : new CommandSettings([.. builder.Permissions.Distinct()], builder.ExpectedVersionRequired, scope, aggregateRoot);

        TextCategory? category = builder.TextCategory ?? (aggregateRoot is null ? null : aggregates.TextsOf(aggregateRoot));

        return (declared, new Texts(category, builder.HiddenProperties.ToHashSet(StringComparer.Ordinal)));
    }

    /// <summary>
    /// Every required permission must be <see cref="PermissionDefinition.AllPlatform"/> or one an aggregate
    /// registered with <c>Permissions(...)</c>, from any source: a definition nobody registered is missing from the
    /// catalogue, so no role could ever grant it.
    /// </summary>
    private static void EnsureRegistered(
        Type configurationType,
        Type requestType,
        IReadOnlyCollection<PermissionDefinition> required,
        AggregateFeatureRegistry aggregates)
    {
        HashSet<PermissionDefinition> registered =
        [
            PermissionDefinition.AllPlatform,
            .. aggregates.ConfiguredTypes.SelectMany(aggregates.PermissionsOf),
        ];

        string[] unregistered =
        [
            .. required
                .Where(permission => !registered.Contains(permission))
                .Select(permission => permission.Permission.Value)
                .Distinct(StringComparer.Ordinal),
        ];

        if (unregistered.Length > 0)
        {
            throw new InvalidOperationException(
                $"Command configuration {configurationType.FullName} requires {string.Join(", ", unregistered)} for "
                + $"{requestType.FullName}, which no aggregate registers. Add it to its aggregate's feature configuration "
                + "with Permissions(...), so the catalogue offers it and a role can grant it.");
        }
    }

    private static bool IsCommand(Type requestType) =>
        requestType.GetInterfaces().Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommand<>));

    private static bool IsClosedConfigurationInterface(Type type) =>
        type.IsGenericType
        && (type.GetGenericTypeDefinition() == typeof(ICommandConfiguration<>)
            || type.GetGenericTypeDefinition() == typeof(ICommandConfiguration<,>));

    private sealed record Texts(TextCategory? Category, IReadOnlySet<string> HiddenProperties);
}
