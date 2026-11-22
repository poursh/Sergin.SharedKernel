using FluentValidation;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>
/// Sends a schedulable task to <paramref name="Targets"/> once, now, with no schedule saved: a one-off run, acting
/// for the caller in the caller's current scope, stored only in run history. No overlap rule (it has no task); the
/// per-item retry applies.
/// </summary>
public sealed record RunTaskOnceCommand(
    string Schema,
    ITaskCommand Template,
    IReadOnlyCollection<TaskScheduleTarget> Targets) : ICommand<TaskScheduleRunStartedResponse>;

internal sealed class RunTaskOnceCommandValidator : AbstractValidator<RunTaskOnceCommand>
{
    public RunTaskOnceCommandValidator(TaskCatalog catalog) =>
        TaskScheduleRules.TemplateAndTargets(this, catalog, request => request.Schema, request => request.Template, request => request.Targets);
}

internal sealed class RunTaskOnceCommandHandler(
    ITaskStoreResolver stores,
    TaskCatalog catalog,
    TaskAuthority authority,
    TaskArgumentCheck argumentCheck,
    TaskRunStarter starter,
    IUserContext caller,
    IAuthorizationScopeContext currentScope,
    IScopePathResolver scopePaths) : ICommandHandler<RunTaskOnceCommand, TaskScheduleRunStartedResponse>
{
    public async Task<ErrorOr<TaskScheduleRunStartedResponse>> Handle(RunTaskOnceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The validator refused a template the catalogue does not hold.
        TaskDescriptor command = catalog.ByRequest(request.Template.GetType())!;

        ErrorOr<Success> allowed = await authority.EnsureMayScheduleAsync(command.RequestType, cancellationToken);

        if (allowed.IsError)
        {
            return allowed.Errors;
        }

        string arguments = TaskCommandFactory.ToArguments(request.Template);

        // The task's own validator over what will be stored, before the run starts: the dialog's fields are not the guarantee.
        List<Error> refused = await argumentCheck.CheckScheduleAsync(command, arguments, request.Targets, cancellationToken);

        if (refused.Count > 0)
        {
            return refused;
        }

        Guid scopeId = currentScope.ScopeId;

        if (await scopePaths.GetPathAsync(scopeId, cancellationToken) is not { } scopePath)
        {
            return Error.NotFound();
        }

        ErrorOr<Guid> started = await starter.StartAsync(
            stores.For(request.Schema),
            new TaskRunStart(
                null,
                TaskRunTrigger.Once,
                caller.Id,
                scopeId,
                scopePath,
                command.Name,
                arguments,
                request.Targets),
            cancellationToken);

        return started.IsError ? started.Errors : new TaskScheduleRunStartedResponse(started.Value);
    }
}
