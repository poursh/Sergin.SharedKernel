using System.Diagnostics.CodeAnalysis;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// Every task (<see cref="ITaskCommand"/>) the sources hold, configured or not, by stable name and by request type,
/// each with the <see cref="TaskSettings"/> the <see cref="CommandConfigurationRegistry"/> read for it. Built once per
/// host beside the registry and refuses, naming every offending request, a malformed name (<see cref="TaskNames.IsValid"/>),
/// a name outside its source's schema and a name two tasks share.
/// </summary>
public sealed class TaskCatalog
{
    private readonly Dictionary<string, TaskDescriptor> byName;
    private readonly Dictionary<Type, TaskDescriptor> byType;

    private TaskCatalog(IReadOnlyCollection<TaskDescriptor> commands)
    {
        Commands = commands;
        byName = commands.ToDictionary(command => command.Name, StringComparer.Ordinal);
        byType = commands.ToDictionary(command => command.RequestType);
    }

    public static TaskCatalog Empty { get; } = new([]);

    public IReadOnlyCollection<TaskDescriptor> Commands { get; }

    public static TaskCatalog FromSources(IEnumerable<ModuleConfigurationSource> sources, CommandConfigurationRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentNullException.ThrowIfNull(registry);

        List<TaskDescriptor> found = [];
        List<string> problems = [];
        HashSet<Type> seen = [];

        foreach (ModuleConfigurationSource source in sources)
        {
            IEnumerable<Type> requests = source.RequestTypes.Concat(source.ConfigurationTypes.SelectMany(RequestsConfiguredBy));

            foreach (Type request in requests.Distinct().Where(seen.Add))
            {
                if (registry.For(request).Task is { } settings && CommandTypes.TargetOf(request) is { } target)
                {
                    Add(request, target, settings, source.Schema, found, problems);
                }
            }
        }

        foreach (IGrouping<string, TaskDescriptor> group in found.GroupBy(command => command.Name, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            problems.Add(
                $"{string.Join(" and ", group.Select(command => command.RequestType.FullName))} are all named \"{group.Key}\"; a name belongs to one task. "
                + "Rename one, or give one another name with Task(task => task.Name(...)).");
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException("A task is named wrongly: " + string.Join(" ", problems));
        }

        return new TaskCatalog(found);
    }

    /// <summary>The task named <paramref name="name"/>, or null.</summary>
    public TaskDescriptor? ByName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return byName.GetValueOrDefault(name);
    }

    /// <summary>The task <paramref name="requestType"/> is, or null when it is not a task.</summary>
    public TaskDescriptor? ByRequest(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return byType.GetValueOrDefault(requestType);
    }

    public bool TryGet(string name, [NotNullWhen(true)] out TaskDescriptor? command)
    {
        command = ByName(name);
        return command is not null;
    }

    private static void Add(
        Type request, CommandTarget target, TaskSettings settings, string? schema, List<TaskDescriptor> found, List<string> problems)
    {
        string name = settings.Name;
        string who = request.FullName ?? request.Name;

        if (!TaskNames.IsValid(name))
        {
            problems.Add(
                $"{who} is named \"{name}\", which is not a valid task name: lower-case segments separated by '.', words inside a segment by '-', e.g. dm.device.read-device-profile.");
            return;
        }

        if (schema is not null && !name.StartsWith(schema + ".", StringComparison.Ordinal))
        {
            problems.Add($"{who} is named \"{name}\", which is outside its module's schema: expected \"{schema}.\" first.");
            return;
        }

        Type? idType = target.Kind == CommandTargetKind.Entity ? request.GetProperty("Id")?.PropertyType : null;

        found.Add(new TaskDescriptor(name, schema, request, target.Root, target.Kind, idType, CommandTypes.IsSelectable(request), settings));
    }

    private static IEnumerable<Type> RequestsConfiguredBy(Type configurationType) =>
        configurationType.GetInterfaces()
            .Where(type => type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ICommandConfiguration<>))
            .Select(type => type.GetGenericArguments()[0]);
}
