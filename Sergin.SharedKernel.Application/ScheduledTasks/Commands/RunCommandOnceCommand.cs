using FluentValidation;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>
/// Sends a schedulable command to <paramref name="Targets"/> once, now, with no schedule saved: a one-off run, acting
/// for the caller in the caller's current scope, stored only in run history. No overlap rule (it has no task); the
/// per-item retry applies.
/// </summary>
public sealed record RunCommandOnceCommand(
    string Schema,
    IBaseCommand Template,
    IReadOnlyCollection<ScheduledTaskTarget> Targets) : ICommand<ScheduledRunStartedResponse>;

internal sealed class RunCommandOnceCommandValidator : AbstractValidator<RunCommandOnceCommand>
{
    public RunCommandOnceCommandValidator(ScheduledCommandCatalog catalog) =>
        ScheduledTaskRules.TemplateAndTargets(this, catalog, request => request.Schema, request => request.Template, request => request.Targets);
}

internal sealed class RunCommandOnceCommandHandler(
    IScheduledTaskStoreResolver stores,
    ScheduledCommandCatalog catalog,
    ScheduledTaskAuthority authority,
    ScheduledRunStarter starter,
    IUserContext caller,
    IAuthorizationScopeContext currentScope,
    IScopePathResolver scopePaths) : ICommandHandler<RunCommandOnceCommand, ScheduledRunStartedResponse>
{
    public async Task<ErrorOr<ScheduledRunStartedResponse>> Handle(RunCommandOnceCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The validator refused a template the catalogue does not hold.
        ScheduledCommand command = catalog.For(request.Template.GetType())!;

        ErrorOr<Success> allowed = await authority.EnsureMayScheduleAsync(command.RequestType, cancellationToken);

        if (allowed.IsError)
        {
            return allowed.Errors;
        }

        Guid scopeId = currentScope.ScopeId;

        if (await scopePaths.GetPathAsync(scopeId, cancellationToken) is not { } scopePath)
        {
            return Error.NotFound();
        }

        ErrorOr<Guid> started = await starter.StartAsync(
            stores.For(request.Schema),
            new ScheduledRunStart(
                null,
                ScheduledRunTrigger.OneOff,
                caller.Id,
                scopeId,
                scopePath,
                command.Name,
                ScheduledCommandFactory.ToArguments(request.Template),
                request.Targets),
            cancellationToken);

        return started.IsError ? started.Errors : new ScheduledRunStartedResponse(started.Value);
    }
}
