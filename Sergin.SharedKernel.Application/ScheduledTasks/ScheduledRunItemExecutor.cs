using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// Executes one attempt of one run item; the engine calls it once per job. The send runs in a fresh scope seeded the
/// way the Blazor dispatcher seeds one: the run's user as a <see cref="DelegatedUserContext"/> (never a system
/// identity, so the user's stored grants are checked live) and the run's scope as the current scope. The command is
/// rebuilt from the run's stored arguments with this item's target, and goes through <see cref="ISender"/>, so every
/// pipeline behavior runs exactly as for a request a person sends.
/// <para>
/// The outcome: success is Succeeded. An <c>ErrorOr</c> error that is not Unexpected (Validation, NotFound,
/// Forbidden, a Failure such as an unknown command or arguments that no longer fit) is Failed with its code and
/// description at once, since trying again cannot change it. An exception, or an Unexpected error, is Retrying and
/// the answer is true (the engine schedules the next attempt) until <see cref="ScheduledTaskOptions.MaxAttempts"/>
/// attempts were made; then it is Failed as <see cref="ScheduledTaskErrors.ExhaustedCode"/>. Whatever the command
/// does, nothing is thrown to the engine. Once an item finishes, the run is completed if it was the last.
/// </para>
/// </summary>
internal sealed partial class ScheduledRunItemExecutor(
    IServiceScopeFactory scopes,
    ScheduledCommandCatalog catalog,
    IScheduledTaskStoreResolver stores,
    IDateTimeProvider clock,
    IOptions<ScheduledTaskOptions> options,
    ILogger<ScheduledRunItemExecutor> logger)
{
    /// <summary>The description of an item whose attempts all started and none finished.</summary>
    internal const string AttemptsLost = "Every attempt started but none finished: the host stopped while it ran.";

    /// <summary>Runs one attempt; true when the engine should try again later.</summary>
    public async Task<bool> ExecuteAsync(string schema, Guid runItemId, CancellationToken ct)
    {
        IScheduledTaskStore store = stores.For(schema);
        ScheduledTaskOptions settings = options.Value;
        DateTime now = clock.UtcNow;

        // Claimed in the database, so a second delivery of the same item (a duplicate job, a recovery's re-enqueue racing
        // a slow delivery) never sends the command a second time.
        if (!await store.TryClaimRunItemAsync(runItemId, now, now - settings.StaleAfter, ct))
        {
            // Finished by an earlier delivery that may have stopped between saving the item and completing the run:
            // completing is idempotent, so finish that step, or the run stays open and the overlap rule skips every later
            // firing of its task. Otherwise it is purged, or another delivery holds it: nothing to do.
            if (await store.GetRunItemAsync(runItemId, ct) is { Item.Status: ScheduledRunItemStatus.Succeeded or ScheduledRunItemStatus.Failed } finished)
            {
                await store.CompleteRunIfFinishedAsync(finished.Run.Id, clock.UtcNow, ct);
            }

            return false;
        }

        if (await store.GetRunItemAsync(runItemId, ct) is not { } found)
        {
            return false;
        }

        (ScheduledTaskRun run, ScheduledTaskRunItem item) = found;

        // Every allowed attempt was claimed and lost (the host stopped mid-send, again and again): nothing is left to try.
        if (item.Attempts > settings.MaxAttempts)
        {
            item.MarkFailed(ScheduledTaskErrors.ExhaustedCode, AttemptsLost, clock.UtcNow);
            await store.SaveAsync(ct);
            await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, ct);

            return false;
        }

        ErrorOr<Success> outcome = await SendAsync(run, item, ct);

        if (outcome.IsError && outcome.FirstError.Type == ErrorType.Unexpected)
        {
            if (item.Attempts < settings.MaxAttempts)
            {
                item.MarkRetrying(outcome.FirstError.Code, outcome.FirstError.Description, clock.UtcNow);
                await store.SaveAsync(ct);
                return true;
            }

            item.MarkFailed(ScheduledTaskErrors.ExhaustedCode, outcome.FirstError.Description, clock.UtcNow);
        }
        else if (outcome.IsError)
        {
            item.MarkFailed(outcome.FirstError.Code, outcome.FirstError.Description, clock.UtcNow);
        }
        else
        {
            item.MarkSucceeded(clock.UtcNow);
        }

        await store.SaveAsync(ct);
        await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, ct);

        return false;
    }

    private async Task<ErrorOr<Success>> SendAsync(ScheduledTaskRun run, ScheduledTaskRunItem item, CancellationToken ct)
    {
        if (!catalog.TryGet(run.CommandName, out ScheduledCommand? command))
        {
            return ScheduledTaskErrors.UnknownCommand;
        }

        ErrorOr<Application.Commands.IBaseCommand> built = ScheduledCommandFactory.Build(command, run.Arguments, item.TargetId, item.RootId);

        if (built.IsError)
        {
            return built.Errors;
        }

        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<UserContextAccessor>().Current = new DelegatedUserContext(run.ActingUserId, run.Id);
            scope.ServiceProvider.GetRequiredService<IAuthorizationScopeContext>().Set(run.ScopeId);

            object? response = await scope.ServiceProvider.GetRequiredService<ISender>().Send(built.Value, ct);

            return response is IErrorOr { IsError: true, Errors: [Error first, ..] } ? first : Result.Success;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            LogAttemptThrew(exception, item.Id, run.Id, run.CommandName, item.Attempts);
            return Error.Unexpected(exception.GetType().FullName ?? exception.GetType().Name, exception.Message);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled run item {RunItemId} of run {RunId} ({CommandName}) threw on attempt {Attempt}.")]
    private partial void LogAttemptThrew(Exception exception, Guid runItemId, Guid runId, string commandName, int attempt);
}
