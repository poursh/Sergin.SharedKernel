using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.DependencyInjection;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Configuration;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Application.Localizations.Validation;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>
/// What a start, a schedule (create or edit) and a Run once check of a task's arguments before anything is written: the
/// task's own permissions as the pipeline checks them, and, while they are published with their reach (so a validator
/// that reads a scoped aggregate sees what it would see when the item runs), the task's own validator over the command
/// built for each target. A task over the collection is built and checked once, with no target. This is the server's
/// backstop for the argument forms; each run item still runs the whole pipeline.
/// </summary>
internal sealed class TaskArgumentCheck(
    IServiceProvider services, CommandConfigurationRegistry configurations, RequestPermissionGate permissionGate)
{
    /// <summary>The refusals: <see cref="Error.Forbidden"/> alone when the task's permissions are not held; empty when it may go on.</summary>
    public async Task<List<Error>> CheckAsync(
        TaskDescriptor command, string arguments, IReadOnlyCollection<TaskTarget> targets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(targets);

        using IDisposable? entered = await permissionGate.EnterAsync(configurations.For(command.RequestType), cancellationToken);

        if (entered is null)
        {
            return [Error.Forbidden()];
        }

        IReadOnlyCollection<TaskTarget> built = command.TargetKind == CommandTargetKind.Collection ? [new TaskTarget(null)] : targets;

        return await ValidateEachAsync(command, arguments, built, cancellationToken);
    }

    /// <summary>As <see cref="CheckAsync"/>, for a schedule's targets, which carry no label.</summary>
    public Task<List<Error>> CheckScheduleAsync(
        TaskDescriptor command, string arguments, IReadOnlyCollection<TaskScheduleTarget> targets, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(targets);

        return CheckAsync(command, arguments, (IReadOnlyCollection<TaskTarget>)[.. targets.Select(target => new TaskTarget(target.TargetId))], cancellationToken);
    }

    /// <summary>
    /// The command built for each target and checked by its own validator, so a bad request fails at the click rather
    /// than as a failed run. The validator is called directly, never through the pipeline: the task guard refuses
    /// a task sent outside a run item. Each run item still runs the whole pipeline.
    /// </summary>
    /// <remarks>
    /// The arguments are shared by every target, so a refusal every target gets (a bad argument) is reported once, as it
    /// is; one only some targets get (a record out of the caller's reach) is reported per target, named by the target's
    /// label when it has one, so the person sees which picked record was refused.
    /// </remarks>
    private async Task<List<Error>> ValidateEachAsync(
        TaskDescriptor command, string arguments, IReadOnlyCollection<TaskTarget> targets, CancellationToken ct)
    {
        var validator = (IValidator?)services.GetService(typeof(IValidator<>).MakeGenericType(command.RequestType));
        List<(TaskTarget Target, List<Error> Errors)> checkedTargets = [];

        foreach (TaskTarget target in targets)
        {
            checkedTargets.Add((target, await ValidateOneAsync(command, arguments, target.TargetId, validator, ct)));
        }

        if (checkedTargets.Count == 0)
        {
            return [];
        }

        List<Error> shared = [.. checkedTargets[0].Errors.Where(error => checkedTargets.All(other => other.Errors.Exists(candidate => Same(candidate, error))))];
        List<Error> refused = [.. shared];

        foreach ((TaskTarget target, List<Error> errors) in checkedTargets)
        {
            refused.AddRange(errors
                .Where(error => !shared.Exists(candidate => Same(candidate, error)))
                .Select(error => target.Label is { } label ? TaskErrors.TargetRefused(error, label) : error));
        }

        return [.. refused.DistinctBy(error => (error.Type, error.Code, error.Description))];
    }

    private async Task<List<Error>> ValidateOneAsync(
        TaskDescriptor command, string arguments, Guid? targetId, IValidator? validator, CancellationToken ct)
    {
        ErrorOr<IBaseCommand> built = TaskCommandFactory.Build(command, arguments, targetId);

        if (built.IsError)
        {
            return [.. built.Errors];
        }

        if (validator is null)
        {
            return [];
        }

        ValidationResult result;

        using (services.GetService<ValidationTexts>()?.Use())
        {
            result = await validator.ValidateAsync(new ValidationContext<IBaseCommand>(built.Value), ct);
        }

        return [.. ValidationFailureErrors.ToErrors(result)];
    }

    private static bool Same(Error one, Error other) =>
        one.Type == other.Type && one.Code == other.Code && one.Description == other.Description;
}
