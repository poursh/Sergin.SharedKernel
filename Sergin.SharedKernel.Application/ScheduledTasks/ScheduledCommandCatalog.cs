using System.Diagnostics.CodeAnalysis;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// Every request whose configuration called <c>Schedulable(name)</c>, by stable name and by request type. Built once
/// per host beside the <see cref="CommandConfigurationRegistry"/> and refuses, naming each offending request, a
/// request that is not an entity or child-entity command (a creation, collection or root target, a system command,
/// an untyped request), one that still requires an expected version (a version stored with the task is stale by
/// the next run; <c>WithoutExpectedVersion()</c> waives it), a malformed name, a name outside its source's schema
/// and a name two requests share.
/// </summary>
public sealed class ScheduledCommandCatalog
{
    private readonly Dictionary<string, ScheduledCommand> byName;
    private readonly Dictionary<Type, ScheduledCommand> byType;

    private ScheduledCommandCatalog(IReadOnlyCollection<ScheduledCommand> commands)
    {
        Commands = commands;
        byName = commands.ToDictionary(command => command.Name, StringComparer.Ordinal);
        byType = commands.ToDictionary(command => command.RequestType);
    }

    public static ScheduledCommandCatalog Empty { get; } = new([]);

    public IReadOnlyCollection<ScheduledCommand> Commands { get; }

    public static ScheduledCommandCatalog FromSources(IEnumerable<ModuleConfigurationSource> sources, CommandConfigurationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(registry);

        List<ScheduledCommand> found = [];
        List<string> problems = [];
        HashSet<Type> seen = [];

        foreach (ModuleConfigurationSource source in sources)
        {
            IEnumerable<Type> requests = source.RequestTypes.Concat(source.ConfigurationTypes.SelectMany(RequestsConfiguredBy));

            foreach (Type request in requests.Distinct().Where(seen.Add))
            {
                CommandSettings settings = registry.For(request);

                if (settings.ScheduleName is not null)
                {
                    Add(request, settings, source.Schema, found, problems);
                }
            }
        }

        foreach (IGrouping<string, ScheduledCommand> group in found.GroupBy(command => command.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            problems.Add(
                $"{string.Join(" and ", group.Select(command => command.RequestType.FullName))} are both schedulable as \"{group.Key}\"; a name belongs to one request.");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("Schedulable() is declared wrongly: " + string.Join(" ", problems));
        }

        return new ScheduledCommandCatalog(found);
    }

    public bool TryGet(string name, [NotNullWhen(true)] out ScheduledCommand? command)
    {
        ArgumentNullException.ThrowIfNull(name);
        return byName.TryGetValue(name, out command);
    }

    /// <summary>The scheduled command for <paramref name="requestType"/>, or null when it is not schedulable.</summary>
    public ScheduledCommand? For(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return byType.GetValueOrDefault(requestType);
    }

    private static void Add(Type request, CommandSettings settings, string? schema, List<ScheduledCommand> found, List<string> problems)
    {
        string name = settings.ScheduleName!;
        string who = request.FullName ?? request.Name;
        CommandTarget? target = CommandTypes.TargetOf(request);
        int before = problems.Count;

        if (!ScheduledCommandNames.IsValid(name))
        {
            problems.Add(
                $"{who} names itself \"{name}\", which is not a valid name: lower-case segments separated by '.', words inside a segment by '-', e.g. dm.devices.read-profile.");
        }
        else if (schema is not null && !name.StartsWith(schema + ".", StringComparison.Ordinal))
        {
            problems.Add($"{who} names itself \"{name}\", which is outside its module's schema: expected \"{schema}.\" first.");
        }

        if (CommandTypes.IsSystem(request))
        {
            problems.Add($"{who} is a system command and cannot be scheduled: it has no target to substitute.");
        }
        else if (target is null)
        {
            problems.Add($"{who} implements no target interface and cannot be scheduled: only an entity or child-entity command names a target.");
        }
        else if (target.Kind is not (CommandTargetKind.Entity or CommandTargetKind.ChildEntity))
        {
            problems.Add(
                $"{who} targets a {target.Kind} (a creation, collection or root-target command) and cannot be scheduled: it has no per-target Id to substitute.");
        }
        else if (settings.RequiresExpectedVersion)
        {
            problems.Add(
                $"{who} requires an expected version and cannot be scheduled: a version stored with the task is stale by the next run. Call WithoutExpectedVersion() as well if the command does not need one.");
        }

        if (problems.Count > before || target is null)
        {
            return;
        }

        Type idType = request.GetProperty("Id")!.PropertyType;
        Type? rootIdType = target.Kind == CommandTargetKind.ChildEntity ? request.GetProperty("RootId")!.PropertyType : null;

        found.Add(new ScheduledCommand(name, schema, request, target.Root, target.Kind, idType, rootIdType, CommandTypes.IsSelectable(request)));
    }

    private static IEnumerable<Type> RequestsConfiguredBy(Type configurationType) =>
        configurationType.GetInterfaces()
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommandConfiguration<>))
            .Select(type => type.GetGenericArguments()[0]);
}
