using FluentValidation;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Localizations;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>
/// Replaces a schedule's arguments, targets and trigger. Version-guarded by its type (<c>ScheduledTask</c> is
/// <c>Versioned()</c>). The editor becomes the delegating user: the next scheduled run acts for them, under their grants.
/// The template must be the command the schedule was saved with.
/// </summary>
public sealed record UpdateScheduledTaskCommand(
    string Schema,
    ScheduledTaskId Id,
    IBaseCommand Template,
    IReadOnlyCollection<ScheduledTaskTarget> Targets,
    string Cron,
    string TimeZoneId) : ICommand<UpdateScheduledTaskCommandResponse>, IUpdateCommand<ScheduledTask, ScheduledTaskId>;

public sealed record UpdateScheduledTaskCommandResponse(Guid Id);

internal sealed class UpdateScheduledTaskCommandValidator : AbstractValidator<UpdateScheduledTaskCommand>
{
    public UpdateScheduledTaskCommandValidator(ScheduledCommandCatalog catalog, ScheduledTaskEngineAccessor engine)
    {
        ScheduledTaskRules.TemplateAndTargets(this, catalog, request => request.Schema, request => request.Template, request => request.Targets);
        ScheduledTaskRules.Trigger(this, engine.Engine, request => request.Cron, request => request.TimeZoneId);
    }
}

/// <summary>
/// Authority check, then the task, then the engine's trigger, then the save. A save that fails (a stale version, a
/// refused record check) puts the trigger back as it was, so the engine never fires on a schedule the row does not hold.
/// </summary>
internal sealed class UpdateScheduledTaskCommandHandler(
    IScheduledTaskStoreResolver stores,
    ScheduledCommandCatalog catalog,
    ScheduledTaskAuthority authority,
    ScheduledTaskEngineAccessor engines,
    IUserContext caller,
    ILogger<UpdateScheduledTaskCommandHandler> logger) : ICommandHandler<UpdateScheduledTaskCommand, UpdateScheduledTaskCommandResponse>
{
    public async Task<ErrorOr<UpdateScheduledTaskCommandResponse>> Handle(UpdateScheduledTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IScheduledTaskEngine engine = engines.Engine;

        IScheduledTaskStore store = stores.For(request.Schema);

        if (await store.GetTaskAsync(request.Id, ignoreScope: false, cancellationToken) is not { } task)
        {
            return Error.NotFound();
        }

        ScheduledCommand command = catalog.For(request.Template.GetType())!;

        if (command.Name != task.CommandName)
        {
            return Error.Validation(
                nameof(UpdateScheduledTaskCommand.Template),
                ScheduledTaskTexts.CommandChanged.Default,
                ErrorText.Of(ScheduledTaskTexts.CommandChanged.Key.Value));
        }

        ErrorOr<Success> allowed = await authority.EnsureMayScheduleAsync(command.RequestType, cancellationToken);

        if (allowed.IsError)
        {
            return allowed.Errors;
        }

        var before = new ScheduledTrigger(request.Schema, task.Id, task.Cron, task.TimeZoneId);
        var after = new ScheduledTrigger(request.Schema, task.Id, request.Cron, request.TimeZoneId);
        Guid? existing = task.EngineTriggerId;
        Guid linked = existing ?? Guid.Empty;

        task.Update(ScheduledCommandFactory.ToArguments(request.Template), request.Targets, request.Cron, request.TimeZoneId, caller.Id);

        // A task with no trigger yet (an engine that lost it is the rebuild's to heal) gets a new one.
        bool changed = await ScheduledTaskTriggerCalls.TryAsync(
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
            return ScheduledTaskErrors.TriggerEngineUnavailable;
        }

        task.LinkTrigger(linked);

        await ScheduledTaskTriggerCalls.SaveOrUndoAsync(
            store,
            () => existing is { } id ? engine.RescheduleAsync(id, before, CancellationToken.None) : engine.RemoveAsync(linked, CancellationToken.None),
            logger,
            "reschedule",
            cancellationToken);

        return new UpdateScheduledTaskCommandResponse(task.Id.Value);
    }
}
