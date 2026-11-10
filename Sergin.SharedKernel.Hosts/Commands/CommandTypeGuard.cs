using System.Reflection;
using Sergin.SharedKernel.Application.Aggregates;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Hosts.Commands;

/// <summary>
/// The shape guard for command types, run at host start over every module's Contracts assembly (local and remote).
/// A typed record must fit the model its interfaces claim: one effect interface at most; <see cref="ISelectable"/> only
/// on an entity or child target; an <c>ISetPropertyCommand</c> with exactly one shown property besides its ids, an
/// <c>ITransitionCommand</c> with none; a restore or purge only on a <c>SoftDeletable()</c> root; an
/// <see cref="IReverseOf{TOther}"/> naming a transition on the same root; and no
/// <see cref="ICommand{TResponse}"/> without an effect unless it is an <see cref="ISystemCommand"/>. One refusal names
/// every offending type.
/// </summary>
/// <remarks>
/// "Shown" here is every public instance property, minus the ids the record's interfaces declare (<c>Id</c>,
/// <c>RootId</c>, <c>ParentId</c>, <c>NewParentId</c>, <c>ExpectedCount</c>) and the ones its configuration marks
/// <c>NotShown(...)</c>.
/// </remarks>
public sealed class CommandTypeGuard(IEnumerable<Assembly> contractsAssemblies)
{
    private static readonly HashSet<string> idNames =
        new(["Id", "RootId", "ParentId", "NewParentId", "ExpectedCount"], StringComparer.Ordinal);

    private static readonly HashSet<Type> setPropertyEffects = [typeof(ISetPropertyCommand<,>), typeof(ISetPropertyCommand<,,,>)];

    private static readonly HashSet<Type> transitionEffects = [typeof(ITransitionCommand<,>), typeof(ITransitionCommand<,,,>)];

    private static readonly HashSet<Type> softDeleteEffects = [typeof(IRestoreCommand<,>), typeof(IPurgeCommand<,>)];

    private readonly Assembly[] assemblies = [.. contractsAssemblies.Distinct()];

    /// <summary>The Contracts assemblies whose requests <see cref="EnsureShaped(CommandConfigurationRegistry, AggregateFeatureRegistry)"/> checks.</summary>
    public IReadOnlyCollection<Assembly> Assemblies => assemblies;

    public void EnsureShaped(CommandConfigurationRegistry registry, AggregateFeatureRegistry aggregates) =>
        Check(assemblies.SelectMany(assembly => assembly.GetTypes()), registry, aggregates, requireTyped: true);

    /// <summary>
    /// As <see cref="EnsureShaped(CommandConfigurationRegistry, AggregateFeatureRegistry)"/>, reading the aggregate
    /// features from the same sources as the registry, remote modules' included.
    /// </summary>
    public void EnsureShaped(CommandConfigurationRegistry registry, IEnumerable<ModuleConfigurationSource> sources)
    {
        ArgumentNullException.ThrowIfNull(sources);

        EnsureShaped(
            registry,
            AggregateFeatureRegistry.FromConfigurationTypes(sources.SelectMany(source => source.AggregateConfigurationTypes).Distinct()));
    }

    /// <summary>Checks the requests among <paramref name="candidates"/>; other types are ignored.</summary>
    public static void Check(
        IEnumerable<Type> candidates, CommandConfigurationRegistry registry, AggregateFeatureRegistry aggregates, bool requireTyped)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(aggregates);

        List<string> problems = [];

        foreach (Type request in candidates.Distinct().Where(IsRequest))
        {
            problems.AddRange(ProblemsOf(request, registry, aggregates, requireTyped));
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                "A request's type does not fit the command model: " + string.Join("; ", problems.Order(StringComparer.Ordinal)) + ".");
        }
    }

    private static List<string> ProblemsOf(
        Type request, CommandConfigurationRegistry registry, AggregateFeatureRegistry aggregates, bool requireTyped)
    {
        CommandTarget? target;

        try
        {
            target = CommandTypes.TargetOf(request);
        }
        catch (InvalidOperationException exception)
        {
            return [exception.Message];
        }

        IReadOnlyList<Type> effects = CommandTypes.EffectsOf(request);
        List<string> problems = [];

        if (effects.Distinct().Count() > 1)
        {
            problems.Add($"{request.FullName} implements more than one effect ({string.Join(", ", effects.Select(Named))}); a request has one");
        }

        if (CommandTypes.IsSelectable(request) && target?.Kind is not (CommandTargetKind.Entity or CommandTargetKind.ChildEntity))
        {
            problems.Add(
                $"{request.FullName} is ISelectable but its target is {target?.Kind.ToString() ?? "none"}; "
                + "only an entity or child target runs once per selected row");
        }

        string[] shown = Shown(request, registry);

        if (effects.Any(setPropertyEffects.Contains) && shown.Length != 1)
        {
            problems.Add(
                $"{request.FullName} is an ISetPropertyCommand with {shown.Length} shown properties besides its ids "
                + $"({string.Join(", ", shown)}); it sets exactly one");
        }

        if (effects.Any(transitionEffects.Contains) && shown.Length > 0)
        {
            problems.Add(
                $"{request.FullName} is an ITransitionCommand with shown properties ({string.Join(", ", shown)}); "
                + "a transition carries none besides its ids");
        }

        if (target is not null && effects.Any(softDeleteEffects.Contains) && !aggregates.For(target.Root).SoftDeletable)
        {
            problems.Add(
                $"{request.FullName} restores or purges {target.Root.FullName}, which is not SoftDeletable(): no deleted row exists");
        }

        problems.AddRange(ReverseProblems(request, target));

        if (requireTyped && effects.Count == 0 && IsCommand(request))
        {
            problems.Add($"{request.FullName} is a command with no effect interface; type it, or mark it ISystemCommand");
        }

        return problems;
    }

    private static IEnumerable<string> ReverseProblems(Type request, CommandTarget? target)
    {
        foreach (Type reversed in request.GetInterfaces()
            .Where(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IReverseOf<>))
            .Select(contract => contract.GetGenericArguments()[0]))
        {
            bool isTransition = CommandTypes.EffectsOf(reversed).Any(transitionEffects.Contains);
            CommandTarget? reversedTarget = SafeTargetOf(reversed);

            if (!isTransition || reversedTarget is null || target is null || reversedTarget.Root != target.Root)
            {
                yield return
                    $"{request.FullName} declares IReverseOf<{reversed.FullName}>, which is not an ITransitionCommand on "
                    + $"{target?.Root.FullName ?? "the same root"}";
            }
        }
    }

    private static CommandTarget? SafeTargetOf(Type type)
    {
        try
        {
            return CommandTypes.TargetOf(type);
        }
        catch (InvalidOperationException)
        {
            // The reversed type is refused on its own, when it is a candidate; here it is simply not a match.
            return null;
        }
    }

    private static string[] Shown(Type request, CommandConfigurationRegistry registry)
    {
        IReadOnlySet<string> hidden = registry.HiddenProperties(request);
        IReadOnlySet<string> declaredByType = CommandTypes.InterfaceMemberNames(request);

        return
        [
            .. request.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Select(property => property.Name)
                .Where(name => !(idNames.Contains(name) && declaredByType.Contains(name)))
                .Where(name => !hidden.Contains(name))
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal),
        ];
    }

    private static string Named(Type effect) => effect.IsGenericType ? effect.Name[..effect.Name.IndexOf('`', StringComparison.Ordinal)] : effect.Name;

    private static bool IsRequest(Type type) =>
        type is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } && typeof(IBaseCommand).IsAssignableFrom(type);

    private static bool IsCommand(Type type) =>
        type.GetInterfaces().Any(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(ICommand<>));
}
