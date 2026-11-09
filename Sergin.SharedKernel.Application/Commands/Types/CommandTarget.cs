namespace Sergin.SharedKernel.Application.Commands.Types;

/// <summary>What a request acts on.</summary>
public enum CommandTargetKind
{
    Collection,
    Root,
    Entity,
    ChildEntity,
}

/// <summary>The aggregate a typed request works on, how it addresses it, and the optional child and structural parent.</summary>
public sealed record CommandTarget(Type Root, CommandTargetKind Kind, Type? Child, Type? Parent);

/// <summary>Reads the target and effect of a request type from the interfaces it implements.</summary>
public static class CommandTypes
{
    /// <summary>The request's target, or null when it implements no target interface.</summary>
    /// <exception cref="InvalidOperationException">The type implements target interfaces with different roots.</exception>
    public static CommandTarget? TargetOf(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        CommandTarget? found = null;
        Type? parent = null;

        foreach (Type contract in requestType.GetInterfaces().Where(t => t.IsGenericType))
        {
            Type definition = contract.GetGenericTypeDefinition();
            Type[] args = contract.GetGenericArguments();

            if (definition == typeof(IUnderParent<,>))
            {
                parent = args[0];
                continue;
            }

            CommandTarget? candidate = definition switch
            {
                _ when definition == typeof(IEntityTarget<,>) => new CommandTarget(args[0], CommandTargetKind.Entity, null, null),
                _ when definition == typeof(IChildEntityTarget<,,,>) => new CommandTarget(args[0], CommandTargetKind.ChildEntity, args[2], null),
                _ when definition == typeof(IRootTarget<,>) => new CommandTarget(args[0], CommandTargetKind.Root, null, null),
                _ when definition == typeof(ICollectionTarget<>) => new CommandTarget(args[0], CommandTargetKind.Collection, null, null),
                _ => null,
            };

            if (candidate is null)
            {
                continue;
            }

            if (found is not null && found.Root != candidate.Root)
            {
                throw new InvalidOperationException(
                    $"{requestType.FullName} implements target interfaces for different roots ({found.Root.Name} and {candidate.Root.Name}).");
            }

            found ??= candidate;
        }

        return found is null ? null : found with { Parent = parent };
    }

    /// <summary>The open generic effect interfaces the request implements, plus <see cref="ISystemCommand"/>.</summary>
    public static IReadOnlyList<Type> EffectsOf(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);

        return [.. requestType.GetInterfaces()
            .Where(IsEffect)
            .Select(t => t.IsGenericType ? t.GetGenericTypeDefinition() : t)];
    }

    public static bool IsSystem(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return typeof(ISystemCommand).IsAssignableFrom(requestType);
    }

    public static bool IsSelectable(Type requestType)
    {
        ArgumentNullException.ThrowIfNull(requestType);
        return typeof(ISelectable).IsAssignableFrom(requestType);
    }

    private static bool IsEffect(Type contract)
    {
        if (!contract.IsGenericType)
        {
            return contract == typeof(ISystemCommand);
        }

        Type definition = contract.GetGenericTypeDefinition();
        return definition.Namespace == typeof(CommandTypes).Namespace
            && definition != typeof(IEntityTarget<,>)
            && definition != typeof(IChildEntityTarget<,,,>)
            && definition != typeof(IRootTarget<,>)
            && definition != typeof(ICollectionTarget<>)
            && definition != typeof(IUnderParent<,>)
            && definition != typeof(IReverseOf<>);
    }
}
