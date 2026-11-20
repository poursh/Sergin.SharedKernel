using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Localizations.Validation;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>One record a task run works on: its id, and its title as the person saw it.</summary>
public sealed record TaskTarget(Guid? TargetId, string? Label = null);

/// <summary>
/// Starts a task over <paramref name="Targets"/>: one run with no schedule and one item per target, acting for the caller
/// in the caller's current scope, in the store of the module that owns the task. Unlike Run once it needs no Manage
/// permission, only the task's own. Answers as soon as the items are enqueued. The template is typed
/// <see cref="ITaskCommand"/>, so a command that is not a task cannot be passed at all.
/// </summary>
public sealed record StartTaskCommand(
    ITaskCommand Template,
    IReadOnlyCollection<TaskTarget> Targets) : ICommand<TaskRunStartedResponse>;

public sealed record TaskRunStartedResponse(TaskRunRef Run);

internal sealed class StartTaskCommandValidator : AbstractValidator<StartTaskCommand>
{
    // A null collection reaches the shared rules as it is, so they refuse it as they refuse any other. A target with no
    // id is refused below; it is mapped to an empty id only so the shared rules can still count and compare the rest.
    private static TaskScheduleTarget[]? ToTargets(IReadOnlyCollection<TaskTarget>? targets) =>
        targets?.Select(target => new TaskScheduleTarget(target.TargetId ?? Guid.Empty)).ToArray();

    public StartTaskCommandValidator(TaskCatalog catalog, IOptions<TaskOptions> options)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);

        int maxTargets = options.Value.MaxTargets;

        TaskDescriptor? CommandOf(ITaskCommand? template) => template is null ? null : catalog.ByRequest(template.GetType());

        // The schema is the catalogue's, never the caller's: the shared rules then only refuse an uncatalogued task.
        TaskScheduleRules.TemplateAndTargets(
            this, catalog, request => CommandOf(request.Template)?.Schema ?? string.Empty, request => request.Template, request => ToTargets(request.Targets), schedulableOnly: false,
            targetsProperty: nameof(StartTaskCommand.Targets));

        RuleFor(request => request.Targets)
            .Must(targets => targets.All(target => target.TargetId is not null))
            .WithMessage(_ => ValidationTexts.Template(TaskTexts.TargetWithoutId))
            .When(request => request.Targets is not null && CommandOf(request.Template) is { TargetKind: CommandTargetKind.Entity });

        // Like the shared rules, these report under the task error codes rather than a property name.
        // A source that is not a module's has no store to start the run in.
        RuleFor(request => request.Template)
            .Must(template => CommandOf(template) is not { Schema: null })
            .OverridePropertyName(TaskErrors.NotSchedulableCode)
            .WithMessage(_ => ValidationTexts.Template(TaskTexts.NotSchedulable));

        RuleFor(request => request.Targets)
            .Must(targets => targets.Count <= maxTargets)
            .OverridePropertyName(TaskErrors.TooManyTargets.Code)
            .WithMessage(_ => ValidationTexts.Template(TaskTexts.TooManyTargets))
            .When(request => request.Targets is not null);
    }
}

internal sealed class StartTaskCommandHandler(
    IServiceProvider services,
    ITaskStoreResolver stores,
    TaskCatalog catalog,
    CommandConfigurationRegistry configurations,
    RequestPermissionGate permissionGate,
    TaskRunStarter starter,
    IUserContext caller,
    IAuthorizationScopeContext currentScope,
    IScopePathResolver scopePaths) : ICommandHandler<StartTaskCommand, TaskRunStartedResponse>
{
    public async Task<ErrorOr<TaskRunStartedResponse>> Handle(StartTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The validator refused a template the catalogue does not hold, or holds with no schema.
        TaskDescriptor command = catalog.ByRequest(request.Template.GetType())!;
        string schema = command.Schema!;

        // The validator refused a missing id for an entity task, and any target for a collection task; this is its backstop.
        if (command.TargetKind == CommandTargetKind.Entity && request.Targets.Any(target => target.TargetId is null))
        {
            return TaskErrors.TargetWithoutId;
        }

        string arguments = TaskCommandFactory.ToArguments(request.Template);

        // The command's own permissions, checked as the pipeline checks them (no Manage: a start is not a schedule), and,
        // while its validator runs, published with their reach as the pipeline publishes them: a validator that reads a
        // scoped aggregate sees what it would see when the item runs.
        using (IDisposable? entered = await permissionGate.EnterAsync(configurations.For(command.RequestType), cancellationToken))
        {
            if (entered is null)
            {
                return Error.Forbidden();
            }

            // A collection task is built and checked once, with no target id.
            IReadOnlyCollection<Guid?> targetIds = command.TargetKind == CommandTargetKind.Collection
                ? [null]
                : [.. request.Targets.Select(target => target.TargetId)];
            List<Error> refused = await ValidateEachAsync(command, arguments, targetIds, cancellationToken);

            if (refused.Count > 0)
            {
                return refused;
            }
        }

        Guid scopeId = currentScope.ScopeId;

        if (await scopePaths.GetPathAsync(scopeId, cancellationToken) is not { } scopePath)
        {
            return Error.NotFound();
        }

        ErrorOr<Guid> started = await starter.StartAsync(
            stores.For(schema),
            new TaskRunStart(
                null, TaskRunTrigger.Manual, caller.Id, scopeId, scopePath, command.Name, arguments,
                [.. request.Targets.Select(target => new TaskScheduleTarget(target.TargetId.GetValueOrDefault()))],
                LabelsOf(request.Targets)),
            cancellationToken);

        return started.IsError ? started.Errors : new TaskRunStartedResponse(new TaskRunRef(schema, started.Value));
    }

    private static Dictionary<Guid, string> LabelsOf(IReadOnlyCollection<TaskTarget> targets)
    {
        var labels = new Dictionary<Guid, string>();

        foreach (TaskTarget target in targets)
        {
            if (target is { TargetId: { } id, Label: { } label })
            {
                labels[id] = label;
            }
        }

        return labels;
    }

    /// <summary>
    /// The command built for each target and checked by its own validator, so a bad request fails at the click rather
    /// than as a failed run. The validator is called directly, never through the pipeline: the task guard refuses
    /// a task sent outside a run item. Each run item still runs the whole pipeline.
    /// </summary>
    private async Task<List<Error>> ValidateEachAsync(
        TaskDescriptor command, string arguments, IReadOnlyCollection<Guid?> targetIds, CancellationToken ct)
    {
        var validator = (IValidator?)services.GetService(typeof(IValidator<>).MakeGenericType(command.RequestType));
        List<Error> refused = [];

        foreach (Guid? targetId in targetIds)
        {
            ErrorOr<IBaseCommand> built = TaskCommandFactory.Build(command, arguments, targetId);

            if (built.IsError)
            {
                refused.AddRange(built.Errors);
                continue;
            }

            if (validator is null)
            {
                continue;
            }

            ValidationResult result;

            using (services.GetService<ValidationTexts>()?.Use())
            {
                result = await validator.ValidateAsync(new ValidationContext<IBaseCommand>(built.Value), ct);
            }

            refused.AddRange(ValidationFailureErrors.ToErrors(result));
        }

        // Arguments are shared by every target, so most refusals repeat once per target; each is reported once.
        return [.. refused.Distinct()];
    }
}
