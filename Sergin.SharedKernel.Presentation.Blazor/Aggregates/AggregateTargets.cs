using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// The roots and child entities the typed requests name, and their id types: what a form picker or an identifier
/// filter can offer. A target is found by its id type, or by name — <c>&lt;Name&gt;Id</c> names the root or child
/// called <c>&lt;Name&gt;</c>. A name two types share resolves to neither.
/// </summary>
internal sealed class AggregateTargets
{
    private readonly Dictionary<Type, Type?> rootIds;
    private readonly Dictionary<Type, Type> childRoots;

    private AggregateTargets(Dictionary<Type, Type?> rootIds, Dictionary<Type, Type> childRoots)
    {
        this.rootIds = rootIds;
        this.childRoots = childRoots;
    }

    public static AggregateTargets From(IEnumerable<Type> requestTypes)
    {
        Dictionary<Type, Type?> roots = [];
        Dictionary<Type, Type> children = [];

        foreach (Type request in requestTypes)
        {
            if (CommandTypes.TargetOf(request) is not { } target)
            {
                continue;
            }

            roots.TryAdd(target.Root, IdTypeOf(target.Root));

            if (target.Parent is { } parent)
            {
                roots.TryAdd(parent, IdTypeOf(parent));
            }

            foreach (Type child in ChildrenNamedBy(request, target))
            {
                children.TryAdd(child, target.Root);
            }
        }

        return new AggregateTargets(roots, children);
    }

    /// <summary>The root whose id type is <paramref name="idType"/>, or null.</summary>
    public Type? RootByIdType(Type idType) =>
        Unique(rootIds.Where(root => root.Value == idType).Select(root => root.Key));

    /// <summary>The child entity whose id type is <paramref name="idType"/>, with its root; null when none.</summary>
    public (Type Child, Type Root)? ChildByIdType(Type idType) =>
        ChildOf(Unique(childRoots.Keys.Where(child => IdTypeOf(child) == idType)));

    /// <summary>The root named by <paramref name="fieldName"/> (<c>ownerId</c> or <c>OwnerId</c> names <c>Owner</c>), or null.</summary>
    public Type? RootByName(string fieldName) =>
        NameOf(fieldName) is { } name ? Unique(rootIds.Keys.Where(root => root.Name == name)) : null;

    /// <summary>The child entity named by <paramref name="fieldName"/>, with its root; null when none.</summary>
    public (Type Child, Type Root)? ChildByName(string fieldName) =>
        NameOf(fieldName) is { } name ? ChildOf(Unique(childRoots.Keys.Where(child => child.Name == name))) : null;

    /// <summary>The camelCase id a list field naming <paramref name="root"/> carries: <c>Manufacturer</c> → <c>manufacturerId</c>.</summary>
    public static string FilterIdOf(Type root) => $"{char.ToLowerInvariant(root.Name[0])}{root.Name[1..]}Id";

    /// <summary>The <c>TId</c> of the <c>IEntity&lt;TId&gt;</c> <paramref name="entity"/> implements, or null.</summary>
    public static Type? IdTypeOf(Type entity) =>
        entity.GetInterfaces()
            .FirstOrDefault(contract => contract.IsGenericType && contract.GetGenericTypeDefinition() == typeof(IEntity<>))
            ?.GetGenericArguments()[0];

    private (Type Child, Type Root)? ChildOf(Type? child) => child is null ? null : (child, childRoots[child]);

    private static string? NameOf(string fieldName) =>
        fieldName.Length > 2 && fieldName.EndsWith("Id", StringComparison.Ordinal)
            ? $"{char.ToUpperInvariant(fieldName[0])}{fieldName[1..^2]}"
            : null;

    private static Type? Unique(IEnumerable<Type> candidates)
    {
        Type[] found = [.. candidates.Take(2)];
        return found.Length == 1 ? found[0] : null;
    }

    // A child-target request names its child; so do an add-child command and a child list query, whose target is the root.
    private static IEnumerable<Type> ChildrenNamedBy(Type request, CommandTarget target)
    {
        if (target.Child is { } child)
        {
            yield return child;
        }

        foreach (Type contract in request.GetInterfaces().Where(contract => contract.IsGenericType))
        {
            Type definition = contract.GetGenericTypeDefinition();

            if (definition == typeof(IChildListOf<,,>) || definition == typeof(IAddChildCommand<,,,>))
            {
                yield return contract.GetGenericArguments()[2];
            }
        }
    }
}
