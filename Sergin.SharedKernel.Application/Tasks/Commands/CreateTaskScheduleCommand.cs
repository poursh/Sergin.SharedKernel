using FluentValidation;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>
/// Saves a schedule in <paramref name="Schema"/>'s module: <paramref name="Template"/> (a schedulable task, its
/// target ids ignored) sent to <paramref name="Targets"/> on <paramref name="Cron"/> in <paramref name="TimeZoneId"/>.
/// The task lands in the caller's current scope and its scheduled runs act for the caller, its delegating user.
/// </summary>
public sealed record CreateTaskScheduleCommand(
    string Schema,
    ITaskCommand Template,
    IReadOnlyCollection<TaskScheduleTarget> Targets,
    string Cron,
    string TimeZoneId) : ICommand<CreateTaskScheduleCommandResponse>, ICreateCommand<TaskSchedule, TaskScheduleId>;

public sealed record CreateTaskScheduleCommandResponse(Guid Id) : ICreatedResponse;

internal sealed class CreateTaskScheduleCommandValidator : AbstractValidator<CreateTaskScheduleCommand>
{
    public CreateTaskScheduleCommandValidator(TaskCatalog catalog, TaskEngineAccessor engine)
    {
        TaskScheduleRules.TemplateAndTargets(this, catalog, request => request.Schema, request => request.Template, request => request.Targets);
        TaskScheduleRules.Trigger(this, engine.Engine, request => request.Cron, request => request.TimeZoneId);
    }
}

/// <summary>
/// Authority check, then the task, then its trigger, then the save: an engine that refuses the trigger leaves nothing
/// saved, and a save that fails removes the trigger it just registered.
/// </summary>
internal sealed class CreateTaskScheduleCommandHandler(
    ITaskStoreResolver stores,
    TaskCatalog catalog,
    TaskAuthority authority,
    TaskArgumentCheck argumentCheck,
    TaskEngineAccessor engines,
    IUserContext caller,
    ILogger<CreateTaskScheduleCommandHandler> logger) : ICommandHandler<CreateTaskScheduleCommand, CreateTaskScheduleCommandResponse>
{
    public async Task<ErrorOr<CreateTaskScheduleCommandResponse>> Handle(CreateTaskScheduleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ITaskEngine engine = engines.Engine;

        // The validator refused a template the catalogue does not hold.
        TaskDescriptor command = catalog.ByRequest(request.Template.GetType())!;

        ErrorOr<Success> allowed = await authority.EnsureMayScheduleAsync(command.RequestType, cancellationToken);

        if (allowed.IsError)
        {
            return allowed.Errors;
        }

        string arguments = TaskCommandFactory.ToArguments(request.Template);

        // The task's own validator over what will be stored, before anything is: the dialog's fields are not the guarantee.
        List<Error> refused = await argumentCheck.CheckScheduleAsync(command, arguments, request.Targets, cancellationToken);

        if (refused.Count > 0)
        {
            return refused;
        }

        ITaskStore store = stores.For(request.Schema);
        var task = TaskSchedule.Create(
            command.Name, arguments, request.Targets, request.Cron, request.TimeZoneId, caller.Id);
        var trigger = new TaskScheduleTrigger(request.Schema, task.Id, request.Cron, request.TimeZoneId);
        Guid triggerId = Guid.Empty;

        if (!await TaskScheduleTriggerCalls.TryAsync(async () => triggerId = await engine.ScheduleAsync(trigger, cancellationToken), logger, "schedule"))
        {
            return TaskErrors.TriggerEngineUnavailable;
        }

        task.LinkTrigger(triggerId);
        store.AddTask(task);

        await TaskScheduleTriggerCalls.SaveOrUndoAsync(
            store, () => engine.RemoveAsync(triggerId, CancellationToken.None), logger, "schedule", cancellationToken);

        return new CreateTaskScheduleCommandResponse(task.Id.Value);
    }
}
