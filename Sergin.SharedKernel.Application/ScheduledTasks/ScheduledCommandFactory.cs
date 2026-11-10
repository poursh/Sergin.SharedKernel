using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sergin.SharedKernel.Application.Commands;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// The argument template: a command's non-target arguments as JSON, and the command rebuilt from them per target.
/// Domain value objects round-trip as their records (<c>{"Note":{"Value":"hello"}}</c>). The target's <c>Id</c> (and
/// a child's <c>RootId</c>) is never stored: <see cref="Build"/> puts it in from the run item. Reading is strict, so
/// arguments a changed command no longer fits (a missing, renamed or retyped property) are
/// <see cref="ScheduledTaskErrors.ArgumentsInvalid"/>, never a command with silently defaulted values.
/// </summary>
public static class ScheduledCommandFactory
{
    // The property names the target interfaces (IEntityTarget, IChildEntityTarget) fix on every request.
    private const string IdProperty = "Id";
    private const string RootIdProperty = "RootId";

    private static readonly JsonSerializerOptions Options = new()
    {
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    /// <summary>The template's arguments as JSON, its <c>Id</c> and <c>RootId</c> left out.</summary>
    public static string ToArguments(IBaseCommand template)
    {
        ArgumentNullException.ThrowIfNull(template);

        JsonObject arguments = JsonSerializer.SerializeToNode(template, template.GetType(), Options)!.AsObject();
        arguments.Remove(IdProperty);
        arguments.Remove(RootIdProperty);

        return arguments.ToJsonString(Options);
    }

    /// <summary>
    /// <paramref name="command"/> built from <paramref name="arguments"/> for one target: its id type made from
    /// <paramref name="targetId"/>, and for a child-entity command its root's from <paramref name="rootId"/>.
    /// </summary>
    public static ErrorOr<IBaseCommand> Build(ScheduledCommand command, string arguments, Guid targetId, Guid? rootId)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(arguments);

        if (command.RootIdType is not null && rootId is null)
        {
            return ScheduledTaskErrors.ArgumentsInvalid;
        }

        try
        {
            if (JsonNode.Parse(arguments) is not JsonObject node)
            {
                return ScheduledTaskErrors.ArgumentsInvalid;
            }

            node[IdProperty] = JsonSerializer.SerializeToNode(Activator.CreateInstance(command.IdType, targetId), command.IdType, Options);

            if (command.RootIdType is not null)
            {
                node[RootIdProperty] = JsonSerializer.SerializeToNode(Activator.CreateInstance(command.RootIdType, rootId!.Value), command.RootIdType, Options);
            }

            return node.Deserialize(command.RequestType, Options) is IBaseCommand built
                ? ErrorOrFactory.From<IBaseCommand>(built)
                : ScheduledTaskErrors.ArgumentsInvalid;
        }
        catch (JsonException)
        {
            return ScheduledTaskErrors.ArgumentsInvalid;
        }
    }
}
