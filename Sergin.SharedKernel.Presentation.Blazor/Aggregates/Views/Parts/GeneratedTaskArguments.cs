using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Presentation.Blazor.Dispatching;
using Sergin.SharedKernel.Presentation.Blazor.Validation;

namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates.Views.Parts;

/// <summary>
/// A task's argument form, written once for its three modes: the values its inputs edit, each field checked by the
/// pipeline's own validator over the task built from them (the same build the start sends, so the two cannot drift), and
/// the start: one run over the targets, the template built from the values with the first target's id, or one run with
/// no record for a task over the collection.
/// </summary>
internal sealed class GeneratedTaskArguments(CommandView command, ISerginFormValidator validator)
{
    public CommandView Command { get; } = command;

    public GeneratedFormValues Values { get; } = new();

    /// <summary>The validator's messages for one argument, over the task built for <paramref name="targetId"/>.</summary>
    public async Task<IEnumerable<string>> ValidateAsync(string propertyName, Guid? targetId) =>
        await Command.Invoker.ValidateAsync(validator, GeneratedViewSupport.TaskTemplateOf(Command, targetId, Values.Values), propertyName);

    /// <summary>Starts the task with the values entered over <paramref name="targets"/> (none for a task over the collection).</summary>
    public Task<ViewSendResult> StartAsync(ISerginDispatcher dispatcher, IReadOnlyCollection<TaskTarget> targets) =>
        Command.Role == CommandRole.CollectionTask
            ? GeneratedViewSupport.StartCollectionAsync(dispatcher, Command, Values.Values)
            : GeneratedViewSupport.StartManyAsync(dispatcher, Command, targets, Values.Values);
}
