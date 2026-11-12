using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Domain;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// The roots and child entities the typed requests name, and their id types: what a form picker or an identifier
/// filter can offer. A target is found by its id type, or by name — <c>&lt;Name&gt;Id</c> names the root or child
/// called <c>&lt;Name&gt;</c>. A name two types share resolves to neither. A picker can only offer a target with a query
/// supplying its options: a root needs an <c>IListOf&lt;Root&gt;</c>, a child an <c>IChildListOf&lt;Root, …, Child&gt;</c>
/// (and its root's list, for the parent picker); <see cref="IsListed(Type)"/> and <see cref="IsListed(Type, Type)"/> answer that.
/// </summary>
internal sealed class AggregateTargets
{
    private readonly Dictionary<Type, Type?> rootIds;
    private readonly Dictionary<Type, Type> childRoots;
    private readonly HashSet<Type> listedRoots;
    private readonly HashSet<(Type Root, Type Child)> listedChildren;

    private AggregateTargets(
        Dictionary<Type, Type?> rootIds, Dictionary<Type, Type> childRoots, HashSet<Type> listedRoots, HashSet<(Type Root, Type Child)> listedChildren)
    {
        this.rootIds = rootIds;
        this.childRoots = childRoots;
        this.listedRoots = listedRoots;
        this.listedChildren = listedChildren;
    }

    public static AggregateTargets From(IEnumerable<Type> requestTypes)
    {
        Dictionary<Type, Type?> roots = [];
        Dictionary<Type, Type> children = [];
        HashSet<Type> listedRoots = [];
        HashSet<(Type Root, Type Child)> listedChildren = [];

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

            CollectLists(request, listedRoots, listedChildren);
        }

        return new AggregateTargets(roots, children, listedRoots, listedChildren);
    }

    /// <summary>Whether a list query (<c>IListOf&lt;<paramref name="root"/>&gt;</c>) supplies a picker of the root.</summary>
    public bool IsListed(Type root) => listedRoots.Contains(root);

    /// <summary>Whether a child list query (<c>IChildListOf</c>) supplies <paramref name="child"/> under <paramref name="root"/>, and the root's list supplies the parent picker.</summary>
    public bool IsListed(Type root, Type child) => listedChildren.Contains((root, child)) && listedRoots.Contains(root);

    private static void CollectLists(Type request, HashSet<Type> listedRoots, HashSet<(Type Root, Type Child)> listedChildren)
    {
        foreach (Type contract in request.GetInterfaces().Where(contract => contract.IsGenericType))
        {
            Type definition = contract.GetGenericTypeDefinition();
            Type[] arguments = contract.GetGenericArguments();

            if (definition == typeof(IListOf<>))
            {
                listedRoots.Add(arguments[0]);
            }
            else if (definition == typeof(IChildListOf<,,>))
            {
                listedChildren.Add((arguments[0], arguments[2]));
            }
        }
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
