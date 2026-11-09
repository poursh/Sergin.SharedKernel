using Microsoft.EntityFrameworkCore.Metadata;
using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// Fails host start when a typed record names an aggregate the EF models do not hold: its root is mapped by no
/// module DbContext, or its child is not reachable from the root through the root's navigations (followed
/// recursively, principal to dependent). Given the local modules' request types only — a remote module's records are
/// mapped in another process. Untyped requests are skipped. Building each context's model needs no database connection.
/// </summary>
public static class CommandTargetMappingGuard
{
    /// <summary>As <see cref="EnsureMapped(IEnumerable{Type}, IReadOnlyCollection{IModel})"/>, over every module context's model.</summary>
    public static void EnsureMapped(IEnumerable<Type> localRequestTypes, IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(localRequestTypes);
        ArgumentNullException.ThrowIfNull(services);

        EnsureMapped(localRequestTypes, AggregateFeatureGuard.ModelsOf(services));
    }

    public static void EnsureMapped(IEnumerable<Type> localRequestTypes, IReadOnlyCollection<IModel> models)
    {
        ArgumentNullException.ThrowIfNull(localRequestTypes);
        ArgumentNullException.ThrowIfNull(models);

        List<string> problems = [];

        foreach (Type request in localRequestTypes.Distinct())
        {
            CommandTarget? target = CommandTypes.TargetOf(request);

            if (target is null)
            {
                continue;
            }

            IEntityType[] roots = [.. models.Select(model => model.FindEntityType(target.Root)).OfType<IEntityType>()];

            if (roots.Length == 0)
            {
                problems.Add($"{request.FullName} targets {target.Root.FullName}, which no module DbContext maps");
                continue;
            }

            if (target.Child is { } child && !roots.Any(root => Reaches(root, child)))
            {
                problems.Add(
                    $"{request.FullName} targets {child.FullName} as a child of {target.Root.FullName}, "
                    + "but no navigation from the root reaches it");
            }
        }

        if (problems.Count > 0)
        {
            throw new InvalidOperationException(
                $"A typed request does not match the EF model: {string.Join("; ", problems)}.");
        }
    }

    private static bool Reaches(IEntityType root, Type child)
    {
        HashSet<IEntityType> seen = [root];
        Queue<IEntityType> pending = new([root]);

        while (pending.TryDequeue(out IEntityType? current))
        {
            // Down the aggregate only: a navigation on the dependent side points back at a principal (the root, or
            // another aggregate a child references), and reaching through it would admit an unrelated entity.
            foreach (INavigation navigation in current.GetNavigations().Where(navigation => !navigation.IsOnDependent))
            {
                IEntityType next = navigation.TargetEntityType;

                if (next.ClrType == child)
                {
                    return true;
                }

                if (seen.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }

        return false;
    }
}
