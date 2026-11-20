using FluentValidation;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Localizations;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>
/// Replaces a schedule's arguments, targets and trigger. Version-guarded by its type (<c>TaskSchedule</c> is
/// <c>Versioned()</c>). The editor becomes the delegating user: the next scheduled run acts for them, under their grants.
/// The template must be the command the schedule was saved with.
/// </summary>
public sealed record UpdateTaskScheduleCommand(
    string Schema,
    TaskScheduleId Id,
    IBaseCommand Template,
    IReadOnlyCollection<TaskScheduleTarget> Targets,
    string Cron,
    string TimeZoneId) : ICommand<UpdateTaskScheduleCommandResponse>, IUpdateCommand<TaskSchedule, TaskScheduleId>;

public sealed record UpdateTaskScheduleCommandResponse(Guid Id);

internal sealed class UpdateTaskScheduleCommandValidator : AbstractValidator<UpdateTaskScheduleCommand>
{
    public UpdateTaskScheduleCommandValidator(TaskCatalog catalog, TaskEngineAccessor engine)
    {
        TaskScheduleRules.TemplateAndTargets(this, catalog, request => request.Schema, request => request.Template, request => request.Targets);
        TaskScheduleRules.Trigger(this, engine.Engine, request => request.Cron, request => request.TimeZoneId);
    }
}

/// <summary>
/// Authority check, then the task, then the engine's trigger, then the save. A save that fails (a stale version, a
/// refused record check) puts the trigger back as it was, so the engine never fires on a schedule the row does not hold.
/// </summary>
internal sealed class UpdateTaskScheduleCommandHandler(
    ITaskStoreResolver stores,
    TaskCatalog catalog,
    TaskAuthority authority,
    TaskEngineAccessor engines,
    IUserContext caller,
    ILogger<UpdateTaskScheduleCommandHandler> logger) : ICommandHandler<UpdateTaskScheduleCommand, UpdateTaskScheduleCommandResponse>
{
    public async Task<ErrorOr<UpdateTaskScheduleCommandResponse>> Handle(UpdateTaskScheduleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        ITaskEngine engine = engines.Engine;

        ITaskStore store = stores.For(request.Schema);

        if (await store.GetTaskAsync(request.Id, ignoreScope: false, cancellationToken) is not { } task)
        {
            return Error.NotFound();
        }

        TaskDescriptor command = catalog.ByRequest(request.Template.GetType())!;

        if (command.Name != task.CommandName)
        {
            return Error.Validation(
                nameof(UpdateTaskScheduleCommand.Template),
                TaskTexts.CommandChanged.Default,
                ErrorText.Of(TaskTexts.CommandChanged.Key.Value));
        }

        ErrorOr<Success> allowed = await authority.EnsureMayScheduleAsync(command.RequestType, cancellationToken);

        if (allowed.IsError)
        {
            return allowed.Errors;
        }

        var before = new TaskScheduleTrigger(request.Schema, task.Id, task.Cron, task.TimeZoneId);
        var after = new TaskScheduleTrigger(request.Schema, task.Id, request.Cron, request.TimeZoneId);
        Guid? existing = task.EngineTriggerId;
        Guid linked = existing ?? Guid.Empty;

        task.Update(TaskCommandFactory.ToArguments(request.Template), request.Targets, request.Cron, request.TimeZoneId, caller.Id);

        // A task with no trigger yet (an engine that lost it is the rebuild's to heal) gets a new one.
        bool changed = await TaskScheduleTriggerCalls.TryAsync(
            async () =>
            {
                if (existing is { } id)
                {
                    await engine.RescheduleAsync(id, after, cancellationToken);
                }
                else
                {
                    linked = await engine.ScheduleAsync(after, cancellationToken);
                }
            },
            logger,
            "reschedule");

        if (!changed)
        {
            return TaskErrors.TriggerEngineUnavailable;
        }

        task.LinkTrigger(linked);

        await TaskScheduleTriggerCalls.SaveOrUndoAsync(
            store,
            () => existing is { } id ? engine.RescheduleAsync(id, before, CancellationToken.None) : engine.RemoveAsync(linked, CancellationToken.None),
            logger,
            "reschedule",
            cancellationToken);

        return new UpdateTaskScheduleCommandResponse(task.Id.Value);
    }
}
