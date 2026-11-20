using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Sergin.SharedKernel.Application.Commands;

namespace Sergin.SharedKernel.Application.Tasks;

/// <summary>
/// The argument template: a command's non-target arguments as JSON, and the command rebuilt from them per target.
/// Domain value objects round-trip as their records (<c>{"Note":{"Value":"hello"}}</c>). The target's <c>Id</c> (and
/// a child's <c>RootId</c>) is never stored: <see cref="Build"/> puts it in from the run item. Reading is strict, so
/// arguments a changed command no longer fits (a missing, renamed or retyped property) are
/// <see cref="TaskErrors.ArgumentsInvalid"/>, never a command with silently defaulted values.
/// </summary>
public static class TaskCommandFactory
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
    /// <paramref name="targetId"/> (a collection task, with no id type, takes none).
    /// </summary>
    public static ErrorOr<IBaseCommand> Build(TaskDescriptor command, string arguments, Guid targetId)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(arguments);

        try
        {
            if (JsonNode.Parse(arguments) is not JsonObject node)
            {
                return TaskErrors.ArgumentsInvalid;
            }

            if (command.IdType is { } idType)
            {
                node[IdProperty] = JsonSerializer.SerializeToNode(Activator.CreateInstance(idType, targetId), idType, Options);
            }

            return node.Deserialize(command.RequestType, Options) is IBaseCommand built
                ? ErrorOrFactory.From<IBaseCommand>(built)
                : TaskErrors.ArgumentsInvalid;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Not only JsonException: an id type with no Guid constructor (MissingMethodException) or a value object
            // whose constructor refuses a stored value throws its own exception, and none of these changes on retry.
            return TaskErrors.ArgumentsInvalid;
        }
    }
}
