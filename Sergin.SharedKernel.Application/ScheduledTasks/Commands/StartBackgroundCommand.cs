using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Localizations.Validation;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>A run's identity outside its module: the schema whose store holds it, and its id.</summary>
public sealed record BackgroundRunRef(string Schema, Guid RunId);

/// <summary>
/// Starts a background command (one whose configuration calls <c>Background()</c>) over <paramref name="Targets"/>: one
/// run with no task and one item per target, acting for the caller in the caller's current scope, in the store of the
/// module that owns the command. Unlike Run once it needs no Manage permission, only the command's own. Answers as soon
/// as the items are enqueued.
/// </summary>
public sealed record StartBackgroundCommand(
    IBaseCommand Template,
    IReadOnlyCollection<ScheduledTaskTarget> Targets) : ICommand<BackgroundRunStartedResponse>;

public sealed record BackgroundRunStartedResponse(BackgroundRunRef Run);

internal sealed class StartBackgroundCommandValidator : AbstractValidator<StartBackgroundCommand>
{
    public StartBackgroundCommandValidator(ScheduledCommandCatalog catalog, IOptions<ScheduledTaskOptions> options)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);

        int maxTargets = options.Value.MaxBackgroundTargets;

        ScheduledCommand? CommandOf(IBaseCommand? template) => template is null ? null : catalog.For(template.GetType());

        // The schema is the catalogue's, never the caller's: the shared rules then only refuse an uncatalogued command.
        ScheduledTaskRules.TemplateAndTargets(
            this, catalog, request => CommandOf(request.Template)?.Schema ?? string.Empty, request => request.Template, request => request.Targets);

        // Like the shared rules, these report under the scheduled-task error codes rather than a property name.
        RuleFor(request => request.Template)
            .Must(template => CommandOf(template) is not { Background: null })
            .OverridePropertyName(ScheduledTaskErrors.NotBackground.Code)
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.NotBackground));

        // A source that is not a module's has no store to start the run in.
        RuleFor(request => request.Template)
            .Must(template => CommandOf(template) is not { Schema: null })
            .OverridePropertyName(ScheduledTaskErrors.NotSchedulable.Code)
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.NotSchedulable));

        RuleFor(request => request.Targets)
            .Must(targets => targets.Count <= maxTargets)
            .OverridePropertyName(ScheduledTaskErrors.TooManyTargets.Code)
            .WithMessage(_ => ValidationTexts.Template(ScheduledTaskTexts.TooManyTargets))
            .When(request => request.Targets is not null);
    }
}

internal sealed class StartBackgroundCommandHandler(
    IServiceProvider services,
    IScheduledTaskStoreResolver stores,
    ScheduledCommandCatalog catalog,
    CommandConfigurationRegistry configurations,
    RequestPermissionGate permissionGate,
    ScheduledRunStarter starter,
    IUserContext caller,
    IAuthorizationScopeContext currentScope,
    IScopePathResolver scopePaths) : ICommandHandler<StartBackgroundCommand, BackgroundRunStartedResponse>
{
    public async Task<ErrorOr<BackgroundRunStartedResponse>> Handle(StartBackgroundCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // The validator refused a template the catalogue does not hold, or holds with no schema.
        ScheduledCommand command = catalog.For(request.Template.GetType())!;
        string schema = command.Schema!;

        string arguments = ScheduledCommandFactory.ToArguments(request.Template);

        // The command's own permissions, checked as the pipeline checks them (no Manage: a start is not a schedule), and,
        // while its validator runs, published with their reach as the pipeline publishes them: a validator that reads a
        // scoped aggregate sees what it would see when the item runs.
        using (IDisposable? entered = await permissionGate.EnterAsync(configurations.For(command.RequestType), cancellationToken))
        {
            if (entered is null)
            {
                return Error.Forbidden();
            }

            List<Error> refused = await ValidateEachAsync(command, arguments, request.Targets, cancellationToken);

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
            new ScheduledRunStart(null, ScheduledRunTrigger.Background, caller.Id, scopeId, scopePath, command.Name, arguments, request.Targets),
            cancellationToken);

        return started.IsError ? started.Errors : new BackgroundRunStartedResponse(new BackgroundRunRef(schema, started.Value));
    }

    /// <summary>
    /// The command built for each target and checked by its own validator, so a bad request fails at the click rather
    /// than as a failed run. The validator is called directly, never through the pipeline: the background guard refuses
    /// a background command sent outside a run item. Each run item still runs the whole pipeline.
    /// </summary>
    private async Task<List<Error>> ValidateEachAsync(
        ScheduledCommand command, string arguments, IReadOnlyCollection<ScheduledTaskTarget> targets, CancellationToken ct)
    {
        var validator = (IValidator?)services.GetService(typeof(IValidator<>).MakeGenericType(command.RequestType));
        List<Error> refused = [];

        foreach (ScheduledTaskTarget target in targets)
        {
            ErrorOr<IBaseCommand> built = ScheduledCommandFactory.Build(command, arguments, target.TargetId, target.RootId);

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
