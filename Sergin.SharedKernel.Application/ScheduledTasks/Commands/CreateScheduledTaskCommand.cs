using FluentValidation;
using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>
/// Saves a schedule in <paramref name="Schema"/>'s module: <paramref name="Template"/> (a schedulable command, its
/// target ids ignored) sent to <paramref name="Targets"/> on <paramref name="Cron"/> in <paramref name="TimeZoneId"/>.
/// The task lands in the caller's current scope and its scheduled runs act for the caller, its delegating user.
/// </summary>
public sealed record CreateScheduledTaskCommand(
    string Schema,
    IBaseCommand Template,
    IReadOnlyCollection<ScheduledTaskTarget> Targets,
    string Cron,
    string TimeZoneId) : ICommand<CreateScheduledTaskCommandResponse>, ICreateCommand<ScheduledTask, ScheduledTaskId>;

public sealed record CreateScheduledTaskCommandResponse(Guid Id) : ICreatedResponse;

internal sealed class CreateScheduledTaskCommandValidator : AbstractValidator<CreateScheduledTaskCommand>
{
    public CreateScheduledTaskCommandValidator(ScheduledCommandCatalog catalog, ScheduledTaskEngineAccessor engine)
    {
        ScheduledTaskRules.TemplateAndTargets(this, catalog, request => request.Schema, request => request.Template, request => request.Targets);
        ScheduledTaskRules.Trigger(this, engine.Engine, request => request.Cron, request => request.TimeZoneId);
    }
}

/// <summary>
/// Authority check, then the task, then its trigger, then the save: an engine that refuses the trigger leaves nothing
/// saved, and a save that fails removes the trigger it just registered.
/// </summary>
internal sealed class CreateScheduledTaskCommandHandler(
    IScheduledTaskStoreResolver stores,
    ScheduledCommandCatalog catalog,
    ScheduledTaskAuthority authority,
    ScheduledTaskEngineAccessor engines,
    IUserContext caller,
    ILogger<CreateScheduledTaskCommandHandler> logger) : ICommandHandler<CreateScheduledTaskCommand, CreateScheduledTaskCommandResponse>
{
    public async Task<ErrorOr<CreateScheduledTaskCommandResponse>> Handle(CreateScheduledTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        IScheduledTaskEngine engine = engines.Engine;

        // The validator refused a template the catalogue does not hold.
        ScheduledCommand command = catalog.For(request.Template.GetType())!;

        ErrorOr<Success> allowed = await authority.EnsureMayScheduleAsync(command.RequestType, cancellationToken);

        if (allowed.IsError)
        {
            return allowed.Errors;
        }

        IScheduledTaskStore store = stores.For(request.Schema);
        var task = ScheduledTask.Create(
            command.Name, ScheduledCommandFactory.ToArguments(request.Template), request.Targets, request.Cron, request.TimeZoneId, caller.Id);
        var trigger = new ScheduledTrigger(request.Schema, task.Id, request.Cron, request.TimeZoneId);
        Guid triggerId = Guid.Empty;

        if (!await ScheduledTaskTriggerCalls.TryAsync(async () => triggerId = await engine.ScheduleAsync(trigger, cancellationToken), logger, "schedule"))
        {
            return ScheduledTaskErrors.TriggerEngineUnavailable;
        }

        task.LinkTrigger(triggerId);
        store.AddTask(task);

        await ScheduledTaskTriggerCalls.SaveOrUndoAsync(
            store, () => engine.RemoveAsync(triggerId, CancellationToken.None), logger, "schedule", cancellationToken);

        return new CreateScheduledTaskCommandResponse(task.Id.Value);
    }
}
