using System.Globalization;
using System.Reflection;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Localizations;
using Sergin.SharedKernel.Application.Tasks;

namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary>
/// What every <see cref="ICommandConfiguration{TCommand}"/> declared, and what every typed record's type implies,
/// keyed by request type. Built once per host from every registered <see cref="ModuleConfigurationSource"/> and read
/// by the permission and expected-version pipeline behaviors on each send. A typed record (one implementing a target
/// interface from <c>Commands.Types</c>) takes its aggregate root from its type, configured or not: it is checked on
/// any scope unless that root is <c>Scoped()</c>, read from the aggregate feature configurations the same sources
/// carry, and a command on an existing entity, child or child collection of a <c>Versioned()</c> root requires an
/// expected version unless its configuration calls <c>WithoutExpectedVersion()</c>; a task (<see cref="ITaskCommand"/>)
/// never does. An untyped request has no root, is
/// checked on the current scope and never requires a version. Refuses two configurations for one request, a
/// configuration without a parameterless constructor, a configuration whose Configure throws, a required permission
/// no aggregate registered, an opt-out where nothing is required, and a task whose <c>Task(...)</c> declares an inactivity
/// timeout outside (0, <see cref="TaskSettings.MaxInactivityTimeout"/>] or a maximum duration that is not above zero or is
/// below the inactivity timeout — each naming the type. Every task, configured or not, carries its
/// <see cref="TaskSettings"/>, its name derived from the schema of the source that holds it (<see cref="TaskNames"/>).
/// </summary>
public sealed class CommandConfigurationRegistry
{
    private static readonly IReadOnlySet<string> noProperties = new HashSet<string>(StringComparer.Ordinal);

    private readonly IReadOnlyDictionary<Type, CommandSettings> settings;
    private readonly IReadOnlyDictionary<Type, Texts> texts;

    private CommandConfigurationRegistry(
        IReadOnlyDictionary<Type, CommandSettings> settings, IReadOnlyDictionary<Type, Texts> texts, IReadOnlyCollection<Type> configuredTypes)
    {
        this.settings = settings;
        this.texts = texts;
        ConfiguredTypes = configuredTypes;
    }

    public static CommandConfigurationRegistry Empty { get; } =
        new(new Dictionary<Type, CommandSettings>(), new Dictionary<Type, Texts>(), []);

    /// <summary>The configured request types.</summary>
    public IReadOnlyCollection<Type> ConfiguredTypes { get; }

    /// <summary>Every request the registry answers for: the configured ones and the typed ones, configured or not.</summary>
    public IReadOnlyCollection<Type> RequestTypes => [.. settings.Keys];

    /// <summary>The settings <paramref name="requestType"/> declared, or a typed record's derived from its type; None for a request neither configured nor typed.</summary>
    public CommandSettings For(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        return settings.TryGetValue(requestType, out CommandSettings? declared) ? declared : CommandSettings.None;
    }

    /// <summary>
    /// The text category <paramref name="requestType"/>'s fields are declared in: the one its configuration names with
    /// <c>Texts(...)</c>, else its typed root's (a typed record with no configuration takes its root's too); null for a request with neither.
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

        // The schema a request's task name starts with: that of the first source holding the request or its configuration.
        Dictionary<Type, string?> schemas = [];

        foreach (ModuleConfigurationSource source in all)
        {
            foreach (Type request in source.RequestTypes.Concat(source.ConfigurationTypes.SelectMany(RequestsConfiguredBy)))
            {
                schemas.TryAdd(request, source.Schema);
            }
        }

        return Build(
            all.SelectMany(source => source.ConfigurationTypes).Distinct(),
            all.SelectMany(source => source.RequestTypes).Distinct(),
            AggregateFeatureRegistry.FromConfigurationTypes(all.SelectMany(source => source.AggregateConfigurationTypes).Distinct()),
            request => schemas.GetValueOrDefault(request));
    }

    /// <summary>Command and aggregate feature configuration types, in any mix.</summary>
    public static CommandConfigurationRegistry FromConfigurationTypes(IEnumerable<Type> configurationTypes)
    {
        ArgumentNullException.ThrowIfNull(configurationTypes);

        return FromSources([ModuleConfigurationSource.FromTypes([.. configurationTypes])]);
    }

    private static CommandConfigurationRegistry Build(
        IEnumerable<Type> configurationTypes, IEnumerable<Type> requestTypes, AggregateFeatureRegistry aggregates, Func<Type, string?> schemaOf)
    {
        List<(Type RequestType, Type ConfigurationType, CommandSettings Settings, Texts Texts)> declared = [];

        foreach (Type configurationType in configurationTypes)
        {
            foreach (Type closedInterface in configurationType.GetInterfaces().Where(IsClosedConfigurationInterface))
            {
                Type requestType = closedInterface.GetGenericArguments()[0];
                (CommandSettings requestSettings, Texts requestTexts) =
                    Run(configurationType, closedInterface, requestType, aggregates, schemaOf(requestType));
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

        var allSettings = declared.ToDictionary(item => item.RequestType, item => item.Settings);
        var allTexts = declared.ToDictionary(item => item.RequestType, item => item.Texts);

        // A typed record with no configuration: its root and version rule from its type, no permissions, the root's text
        // category, and for a task every default.
        foreach (Type requestType in requestTypes.Where(type => !allSettings.ContainsKey(type)))
        {
            CommandTarget? target = CommandTypes.TargetOf(requestType);

            if (target is null)
            {
                continue;
            }

            AggregateFeatures features = aggregates.For(target.Root);

            allSettings[requestType] = new CommandSettings(
                [],
                RequiresVersionByType(requestType, target, features),
                ScopeOf(target.Root, features),
                target.Root,
                CommandTypes.IsTask(requestType) ? DefaultTask(requestType, target.Root, schemaOf(requestType)) : null);
            allTexts[requestType] = new Texts(aggregates.TextsOf(target.Root), noProperties);
        }

        return new CommandConfigurationRegistry(allSettings, allTexts, [.. declared.Select(item => item.RequestType)]);
    }

    internal static bool IsConfigurationType(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false }
        && type.GetInterfaces().Any(IsClosedConfigurationInterface);

    private static (CommandSettings Settings, Texts Texts) Run(
        Type configurationType,
        Type closedInterface,
        Type requestType,
        AggregateFeatureRegistry aggregates,
        string? schema)
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

        CommandTarget? target = CommandTypes.TargetOf(requestType);
        Type? aggregateRoot = target?.Root;
        AggregateFeatures features = aggregateRoot is null ? AggregateFeatures.None : aggregates.For(aggregateRoot);

        EnsureRegistered(configurationType, requestType, builder.Permissions, aggregates);

        bool requiredByType = target is not null && RequiresVersionByType(requestType, target, features);

        if (builder.ExpectedVersionWaived && !requiredByType)
        {
            throw new InvalidOperationException(
                $"Command configuration {configurationType.FullName} calls WithoutExpectedVersion() for {requestType.FullName}, "
                + "which would not require an expected version anyway: only a command on an existing entity, child or child "
                + "collection of a Versioned() root does, and never a task. Drop WithoutExpectedVersion().");
        }

        TaskSettings? task = aggregateRoot is not null && CommandTypes.IsTask(requestType)
            ? TaskOf(configurationType, requestType, builder.TaskDeclaration, DefaultTask(requestType, aggregateRoot, schema))
            : null;

        bool requiresVersion = requiredByType && !builder.ExpectedVersionWaived;
        PermissionScope scope = ScopeOf(aggregateRoot, features);

        CommandSettings declared = builder.Permissions.Count == 0 && !requiresVersion && aggregateRoot is null
            ? CommandSettings.None
            : new CommandSettings([.. builder.Permissions.Distinct()], requiresVersion, scope, aggregateRoot, task);

        TextCategory? category = builder.TextCategory ?? (aggregateRoot is null ? null : aggregates.TextsOf(aggregateRoot));

        return (declared, new Texts(category, builder.HiddenProperties.ToHashSet(StringComparer.Ordinal)));
    }

    private static TaskSettings DefaultTask(Type requestType, Type root, string? schema) =>
        new(TaskNames.Derive(schema, root, requestType), TaskSettings.DefaultInactivityTimeout, MaxDuration: null, Cancelable: true, Schedulable: false);

    /// <summary>What <c>Task(...)</c> declared over <paramref name="defaults"/>, refused when a timeout is out of range.</summary>
    private static TaskSettings TaskOf(Type configurationType, Type requestType, TaskBuilder declared, TaskSettings defaults)
    {
        string who = $"Command configuration {configurationType.FullName} declares Task(...) for {requestType.FullName} ";
        TimeSpan inactivity = declared.DeclaredInactivityTimeout ?? defaults.InactivityTimeout;

        if (inactivity <= TimeSpan.Zero || inactivity > TaskSettings.MaxInactivityTimeout)
        {
            throw new InvalidOperationException(
                who + $"with an inactivity timeout of {Describe(inactivity)}, which must be above zero and at most "
                + $"{Describe(TaskSettings.MaxInactivityTimeout)}.");
        }

        if (declared.DeclaredMaxDuration is { } max && (max <= TimeSpan.Zero || max < inactivity))
        {
            throw new InvalidOperationException(
                who + $"with a maximum duration of {Describe(max)}, which must be above zero and at least its inactivity "
                + $"timeout of {Describe(inactivity)}.");
        }

        return defaults with
        {
            Name = declared.DeclaredName ?? defaults.Name,
            InactivityTimeout = inactivity,
            MaxDuration = declared.DeclaredMaxDuration,
            Cancelable = !declared.IsNotCancelable,
            Schedulable = declared.IsSchedulable,
        };
    }

    /// <summary>A whole number of hours or minutes in words ("1 hour", "90 minutes"); anything else as a TimeSpan.</summary>
    private static string Describe(TimeSpan span) => span switch
    {
        { Ticks: > 0 } when span.Ticks % TimeSpan.TicksPerHour == 0 => Plural((long)span.TotalHours, "hour"),
        { Ticks: > 0 } when span.Ticks % TimeSpan.TicksPerMinute == 0 => Plural((long)span.TotalMinutes, "minute"),
        _ => span.ToString("c", CultureInfo.InvariantCulture),
    };

    private static string Plural(long count, string unit) =>
        string.Create(CultureInfo.InvariantCulture, $"{count} {unit}{(count == 1 ? string.Empty : "s")}");

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

    /// <summary>
    /// The version rule a typed request's type implies: a command whose target is an existing root, a child, or a
    /// root's child collection on a <c>Versioned()</c> root. Creation, criteria, a clone's source, tasks and queries carry
    /// none: a task runs later than the page read the record, so a version would be stale.
    /// </summary>
    private static bool RequiresVersionByType(Type requestType, CommandTarget target, AggregateFeatures features) =>
        IsCommand(requestType)
        && !CommandTypes.IsTask(requestType)
        && target.Kind is CommandTargetKind.Root or CommandTargetKind.Entity or CommandTargetKind.ChildEntity
        && !CommandTypes.EffectsOf(requestType).Contains(typeof(ICloneCommand<,>))
        && features.Versioned;

    // Untyped: the current scope. Typed: the current scope for a scoped aggregate, whose records live in scopes;
    // any scope for another, whose records belong to none.
    private static PermissionScope ScopeOf(Type? aggregateRoot, AggregateFeatures features) =>
        aggregateRoot is not null && !features.Scoped ? PermissionScope.Any : PermissionScope.Current;

    private static bool IsCommand(Type requestType) =>
        requestType.GetInterfaces().Any(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommand<>));

    private static IEnumerable<Type> RequestsConfiguredBy(Type configurationType) =>
        configurationType.GetInterfaces().Where(IsClosedConfigurationInterface).Select(type => type.GetGenericArguments()[0]);

    private static bool IsClosedConfigurationInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommandConfiguration<>);

    private sealed record Texts(TextCategory? Category, IReadOnlySet<string> HiddenProperties);
}
