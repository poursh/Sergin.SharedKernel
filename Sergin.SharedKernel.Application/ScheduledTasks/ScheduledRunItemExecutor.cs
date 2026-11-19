using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.ScheduledTasks.Activity;
using Sergin.SharedKernel.Application.ScheduledTasks.Commands;
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
/// <para>
/// While the command runs, a heartbeat stamps the item's activity every third of
/// <see cref="ScheduledTaskOptions.StaleAfter"/>, so the recovery never re-sends a command that is still running. A
/// background command whose declared timeout passes is cancelled and Failed as
/// <see cref="ScheduledTaskErrors.TimedOutCode"/>, never retried; a cancellation of the engine's own token (a host
/// shutdown) is not a timeout, and propagates, leaving the item Running for the recovery. A cancellation the command
/// raised with neither token cancelled (an HTTP client's own timeout) is an exception like any other, retried.
/// </para>
/// <para>
/// A cancel of the run reaches the item wherever it was asked (another circuit, another process) through the heartbeat,
/// which ticks every <c>min(StaleAfter / 3, HeartbeatInterval)</c> and reads the run's cancel request: the send's token is
/// cancelled and the item ends Cancelled, final, beating a timeout; a send that returned first keeps its outcome. A cancel
/// that landed between the claim and the read sends nothing, and a would-be retry of a run asked to cancel ends Cancelled.
/// </para>
/// </summary>
internal sealed partial class ScheduledRunItemExecutor(
    IServiceScopeFactory scopes,
    ScheduledCommandCatalog catalog,
    IScheduledTaskStoreResolver stores,
    IDateTimeProvider clock,
    IOptions<ScheduledTaskOptions> options,
    IRunNotifier notifier,
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
            // Finished by an earlier delivery that may have stopped between saving the item and completing the run, or
            // cancelled: completing is idempotent, so finish that step, or the run stays open and the overlap rule skips
            // every later firing of its task. Otherwise it is purged, another delivery holds it, or its run is being
            // cancelled (the cancel moves it on): nothing to do, and the command is never sent.
            if (await store.GetRunItemAsync(runItemId, ct) is { Item.Status: ScheduledRunItemStatus.Succeeded or ScheduledRunItemStatus.Failed or ScheduledRunItemStatus.Cancelled } finished)
            {
                await store.CompleteRunIfFinishedAsync(finished.Run.Id, clock.UtcNow, ct);
                Notify(store, finished.Run);
            }

            return false;
        }

        if (await store.GetRunItemAsync(runItemId, ct) is not { } found)
        {
            return false;
        }

        (ScheduledTaskRun run, ScheduledTaskRunItem item) = found;

        // The claim moved the item to Running.
        Notify(store, run);

        // A cancel landed between the claim and the read: the claim refuses a cancelled run's items, this one was already
        // past it. Final, and nothing is sent.
        if (run.CancelRequestedAtUtc is not null)
        {
            await FinishCancelledAsync(store, run, item, ct);
            return false;
        }

        // Every allowed attempt was claimed and lost (the host stopped mid-send, again and again): nothing is left to try.
        if (item.Attempts > settings.MaxAttempts)
        {
            item.MarkFailed(ScheduledTaskErrors.ExhaustedCode, AttemptsLost, clock.UtcNow);
            await store.SaveAsync(ct);
            await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, ct);
            Notify(store, run);

            return false;
        }

        ScheduledCommand? command = catalog.TryGet(run.CommandName, out ScheduledCommand? declared) ? declared : null;
        TimeSpan? timeout = command?.Background?.Timeout;

        // The heartbeat keeps the item's activity fresh while the command runs, so the recovery never takes a command
        // that runs longer than StaleAfter for a lost one, and stops the command (cancelSource) once it reads that the run
        // was asked to cancel. It stops, and is awaited, before the outcome is saved.
        using var stopHeartbeat = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var cancelSource = new CancellationTokenSource();
        using var sendSource = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, cancelSource.Token);

        if (timeout is { } limit)
        {
            timeoutSource.CancelAfter(limit);
        }

        TimeSpan interval = settings.StaleAfter / 3 < settings.HeartbeatInterval ? settings.StaleAfter / 3 : settings.HeartbeatInterval;
        Task heartbeat = HeartbeatAsync(schema, item.Id, interval, cancelSource.CancelAsync, stopHeartbeat.Token);
        ErrorOr<Success> outcome;
        bool cancelled;
        bool timedOut;

        try
        {
            outcome = await SendAsync(run, item, command, sendSource.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && (cancelSource.IsCancellationRequested || timeoutSource.IsCancellationRequested))
        {
            // The run's cancel or the command's own timeout, not a host shutdown: final either way; which one is settled below.
            outcome = Error.Unexpected();
        }
        finally
        {
            // Read before the heartbeat stops, so only a cancel that fired while the send ran counts; a send that returned
            // first keeps its own outcome.
            cancelled = cancelSource.IsCancellationRequested;
            timedOut = timeout.HasValue && timeoutSource.IsCancellationRequested;
            await stopHeartbeat.CancelAsync();
            await heartbeat;
        }

        // However the send ended once the cancel fired (a wrapped cancellation, an error, even a late success), the run was
        // cancelled under it: final, never retried, and it beats a timeout that fired too. A host shutdown is neither.
        if (cancelled && !ct.IsCancellationRequested)
        {
            await FinishCancelledAsync(store, run, item, ct);
            return false;
        }

        // However the send ended once the timeout fired, the command outran its timeout: record that, never a retry.
        if (timedOut && !ct.IsCancellationRequested)
        {
            outcome = ScheduledTaskErrors.TimedOut(timeout!.Value);
        }

        if (outcome.IsError && outcome.FirstError.Type == ErrorType.Unexpected)
        {
            // A retry the run's cancel would refuse: the cancel committed while the send ran and no heartbeat read it yet.
            // Asked once more here, so the item ends Cancelled rather than Retrying with no delivery left to finish it.
            if ((await store.TouchRunItemAsync(item.Id, clock.UtcNow, ct)).CancelRequested)
            {
                await FinishCancelledAsync(store, run, item, ct);
                return false;
            }

            if (item.Attempts < settings.MaxAttempts)
            {
                item.MarkRetrying(outcome.FirstError.Code, outcome.FirstError.Description, clock.UtcNow);
                await store.SaveAsync(ct);
                Notify(store, run);
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
        Notify(store, run);

        return false;
    }

    private async Task FinishCancelledAsync(IScheduledTaskStore store, ScheduledTaskRun run, ScheduledTaskRunItem item, CancellationToken ct)
    {
        item.MarkCancelled(clock.UtcNow);
        await store.SaveAsync(ct);
        await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, ct);
        Notify(store, run);
    }

    // After the save it reports has committed; a subscriber's failure never reaches the executor.
    private void Notify(IScheduledTaskStore store, ScheduledTaskRun run) =>
        notifier.Publish(new RunChanged(new BackgroundRunRef(store.Schema, run.Id), run.ActingUserId));

    private async Task<ErrorOr<Success>> SendAsync(ScheduledTaskRun run, ScheduledTaskRunItem item, ScheduledCommand? command, CancellationToken ct)
    {
        if (command is null)
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
            scope.ServiceProvider.GetRequiredService<RunItemContext>().Set(item.Id);

            object? response = await scope.ServiceProvider.GetRequiredService<ISender>().Send(built.Value, ct);

            return response is IErrorOr { IsError: true, Errors: [Error first, ..] } ? first : Result.Success;
        }
        // A cancellation is the send's own only when its token caused it (the host stopping, or the timeout). One the
        // command raised by itself (an HTTP client's timeout, say) is a failed attempt like any other exception.
        catch (Exception exception) when (exception is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            LogAttemptThrew(exception, item.Id, run.Id, run.CommandName, item.Attempts);
            return Error.Unexpected(exception.GetType().FullName ?? exception.GetType().Name, exception.Message);
        }
    }

    /// <summary>
    /// Stamps the item's activity every <paramref name="interval"/> until <paramref name="stop"/> is cancelled, and calls
    /// <paramref name="cancelSend"/> once, the first time a touch reads that the run was asked to cancel. Each touch
    /// opens its own scope and store: the executor's context is busy with the send and is not thread-safe. A failed touch
    /// is logged and the next tick tries again; nothing is thrown.
    /// </summary>
    private async Task HeartbeatAsync(string schema, Guid runItemId, TimeSpan interval, Func<Task> cancelSend, CancellationToken stop)
    {
        using var timer = new PeriodicTimer(interval);
        bool cancelSent = false;

        try
        {
            while (await timer.WaitForNextTickAsync(stop))
            {
                try
                {
                    await using AsyncServiceScope scope = scopes.CreateAsyncScope();
                    IScheduledTaskStore store = scope.ServiceProvider.GetRequiredService<IScheduledTaskStoreResolver>().For(schema);
                    RunItemHeartbeat beat = await store.TouchRunItemAsync(runItemId, clock.UtcNow, stop);

                    if (beat.CancelRequested && !cancelSent)
                    {
                        cancelSent = true;
                        await cancelSend();
                    }
                }
                catch (Exception exception) when (!stop.IsCancellationRequested)
                {
                    // Any failure, a cancellation the stop token did not cause included: a faulted heartbeat would throw
                    // from the executor's finally and replace the outcome.
                    LogHeartbeatFailed(exception, runItemId);
                }
            }
        }
        catch (Exception) when (stop.IsCancellationRequested)
        {
            // Stopped: the send finished, or the host is stopping. Whatever a touch in flight threw then, a cancellation
            // or the store's own failure, is dropped: a faulted heartbeat would replace the outcome.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "The heartbeat of scheduled run item {RunItemId} failed; the next tick tries again.")]
    private partial void LogHeartbeatFailed(Exception exception, Guid runItemId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Scheduled run item {RunItemId} of run {RunId} ({CommandName}) threw on attempt {Attempt}.")]
    private partial void LogAttemptThrew(Exception exception, Guid runItemId, Guid runId, string commandName, int attempt);
}
