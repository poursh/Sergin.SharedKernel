using System.Runtime.CompilerServices;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Application.Tasks.Activity;
using Sergin.SharedKernel.Application.Tasks.Commands;
using Sergin.SharedKernel.Application.Securities.Authorization;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Application.Times;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Application.Tasks;

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
/// the answer is true (the engine schedules the next attempt) until <see cref="TaskOptions.MaxAttempts"/>
/// attempts were made; then it is Failed as <see cref="TaskErrors.ExhaustedCode"/>. Whatever the command
/// does, nothing is thrown to the engine. Once an item finishes, the run is completed if it was the last.
/// </para>
/// <para>
/// While the command runs, a heartbeat stamps the item's activity every third of
/// <see cref="TaskOptions.StaleAfter"/>, so the recovery never re-sends a command that is still running. The heartbeat
/// proves the worker alive, not the task: it never resets the task's inactivity timeout.
/// </para>
/// <para>
/// A task runs under its <see cref="TaskTimeouts"/>: the inactivity timeout, armed when the send starts and re-armed by
/// every <see cref="ITaskProgress"/> call, and its <c>MaxDuration</c>, when it declares one, armed once. The one that
/// expires first cancels the send, and the item is Failed as <see cref="TaskErrors.TimedOutCode"/> or
/// <see cref="TaskErrors.MaxDurationExceededCode"/>, never retried, however the send ended: a handler that ignores its
/// token and returns a success late is still timed out. A cancellation of the engine's own token (a host shutdown) is not
/// a timeout, and propagates, leaving the item Running for the recovery. A cancellation the command raised with no token
/// cancelled (an HTTP client's own timeout) is an exception like any other, retried.
/// </para>
/// <para>
/// A cancel of the run reaches the item wherever it was asked (another circuit, another process) through the heartbeat,
/// which ticks every <c>min(StaleAfter / 3, HeartbeatInterval)</c> and reads the run's cancel request: the send's token is
/// cancelled and the item ends Cancelled, final, beating a timeout; a send that returned first keeps its outcome. The send's
/// end is read before the heartbeat stops, so a beat that fires the cancel in the instant after the send returned, before
/// that read, records Cancelled even though the command finished. A cancel that landed between the claim and the read
/// sends nothing, and a would-be retry of a run asked to cancel ends Cancelled (asked again after the Retrying save, and a
/// later delivery that finds a waiting item of a cancelled run cancels it). A command that finishes within one heartbeat
/// interval cannot be stopped once running; its run's items not yet started still are.
/// </para>
/// <para>
/// A command reports progress through <see cref="ITaskProgress"/>, which writes it, throttled, while the command runs; the
/// last value reported is read from the send's scope when the send ends and saved with the outcome, whatever it is.
/// </para>
/// </summary>
internal sealed partial class TaskRunItemExecutor(
    IServiceScopeFactory scopes,
    TaskCatalog catalog,
    ITaskStoreResolver stores,
    IDateTimeProvider clock,
    IOptions<TaskOptions> options,
    IRunNotifier notifier,
    ILogger<TaskRunItemExecutor> logger)
{
    /// <summary>The description of an item whose attempts all started and none finished.</summary>
    internal const string AttemptsLost = "Every attempt started but none finished: the host stopped while it ran.";

    /// <summary>Runs one attempt; true when the engine should try again later.</summary>
    public async Task<bool> ExecuteAsync(string schema, Guid runItemId, CancellationToken ct)
    {
        ITaskStore store = stores.For(schema);
        TaskOptions settings = options.Value;
        DateTime now = clock.UtcNow;

        // Claimed in the database, so a second delivery of the same item (a duplicate job, a recovery's re-enqueue racing
        // a slow delivery) never sends the command a second time.
        if (!await store.TryClaimRunItemAsync(runItemId, now, now - settings.StaleAfter, ct))
        {
            // Finished by an earlier delivery that may have stopped between saving the item and completing the run, or
            // cancelled: completing is idempotent, so finish that step, or the run stays open and the overlap rule skips
            // every later firing of its task. A waiting item of a run asked to cancel (a retry saved just after the cancel
            // moved the run's waiting items on) is cancelled here, so the run completes without waiting for the recovery.
            // Otherwise it is purged or another delivery holds it: nothing to do, and the command is never sent.
            switch (await store.GetRunItemAsync(runItemId, ct))
            {
                case { Item.Status: TaskRunItemStatus.Succeeded or TaskRunItemStatus.Failed or TaskRunItemStatus.Cancelled } finished:
                    await store.CompleteRunIfFinishedAsync(finished.Run.Id, clock.UtcNow, ct);
                    Notify(store, finished.Run);
                    break;
                case { Item.Status: TaskRunItemStatus.Pending or TaskRunItemStatus.Retrying, Run.CancelRequestedAtUtc: not null } waiting:
                    await FinishCancelledAsync(store, waiting.Run, waiting.Item, ct);
                    break;
            }

            return false;
        }

        if (await store.GetRunItemAsync(runItemId, ct) is not { } found)
        {
            return false;
        }

        (TaskRun run, TaskRunItem item) = found;

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
            item.MarkFailed(TaskErrors.ExhaustedCode, AttemptsLost, clock.UtcNow);
            await store.SaveAsync(ct);
            await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, ct);
            Notify(store, run);

            return false;
        }

        TaskDescriptor? command = catalog.ByName(run.CommandName);

        // The heartbeat keeps the item's activity fresh while the command runs, so the recovery never takes a command
        // that runs longer than StaleAfter for a lost one, and stops the command (cancelSource) once it reads that the run
        // was asked to cancel. It stops, and is awaited, before the outcome is saved.
        using var stopHeartbeat = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var cancelSource = new CancellationTokenSource();

        // Armed here, as the send starts; the send's scope reaches them through RunItemContext, so each progress call
        // re-arms the inactivity timeout. The heartbeat never touches them.
        using TaskTimeouts? timeouts = command is null ? null : new TaskTimeouts(command.Settings);
        using var sendSource = CancellationTokenSource.CreateLinkedTokenSource(
            ct, cancelSource.Token, timeouts?.Token ?? CancellationToken.None);

        TimeSpan interval = settings.StaleAfter / 3 < settings.HeartbeatInterval ? settings.StaleAfter / 3 : settings.HeartbeatInterval;
        Task heartbeat = HeartbeatAsync(schema, item.Id, interval, cancelSource.CancelAsync, stopHeartbeat.Token);
        ErrorOr<Success> outcome;
        bool cancelled;
        bool inactive;
        bool capped;
        var progress = new StrongBox<int?>();

        try
        {
            outcome = await SendAsync(schema, run, item, command, timeouts, progress, sendSource.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && sendSource.IsCancellationRequested)
        {
            // The run's cancel or the command's own timeout, not a host shutdown: final either way; which one is settled below.
            outcome = Error.Unexpected();
        }
        finally
        {
            // Read before the heartbeat stops, so only a cancel that fired while the send ran counts; a send that returned
            // first keeps its own outcome. The timeouts are read at the same point: one that expired while the send ran
            // stands, even under a success the handler returned after it.
            cancelled = cancelSource.IsCancellationRequested;
            inactive = timeouts?.InactivityExpired == true;
            capped = timeouts?.MaxDurationExpired == true;
            await stopHeartbeat.CancelAsync();
            await heartbeat;
        }

        // The last value the command reported, which the throttle may have held back: kept on the item by whichever
        // save follows (succeeded, failed, retrying, timed out or cancelled), so the item shows it whatever the outcome.
        if (progress.Value is { } percent)
        {
            item.SetProgress(percent);
        }

        // However the send ended once the cancel fired (a wrapped cancellation, an error, even a late success), the run was
        // cancelled under it: final, never retried, and it beats a timeout that fired too. A host shutdown is neither.
        if (cancelled && !ct.IsCancellationRequested)
        {
            await FinishCancelledAsync(store, run, item, ct);
            return false;
        }

        // However the send ended once a timeout expired (a wrapped cancellation, an error, a late success), the task outran
        // it: record which, never a retry.
        if (inactive && timeouts is not null && !ct.IsCancellationRequested)
        {
            outcome = TaskErrors.TimedOut(timeouts.InactivityTimeout);
        }
        else if (capped && timeouts?.MaxDuration is { } cap && !ct.IsCancellationRequested)
        {
            outcome = TaskErrors.MaxDurationExceeded(cap);
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

                // A cancel that committed between the read above and this save left the item Retrying under a cancelled
                // run, which no delivery would claim: asked once more, so it ends Cancelled now rather than at the recovery.
                if ((await store.TouchRunItemAsync(item.Id, clock.UtcNow, ct)).CancelRequested)
                {
                    await FinishCancelledAsync(store, run, item, ct);
                    return false;
                }

                Notify(store, run);
                return true;
            }

            item.MarkFailed(TaskErrors.ExhaustedCode, outcome.FirstError.Description, clock.UtcNow);
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

    private async Task FinishCancelledAsync(ITaskStore store, TaskRun run, TaskRunItem item, CancellationToken ct)
    {
        item.MarkCancelled(clock.UtcNow);
        await store.SaveAsync(ct);
        await store.CompleteRunIfFinishedAsync(run.Id, clock.UtcNow, ct);
        Notify(store, run);
    }

    // After the save it reports has committed; a subscriber's failure never reaches the executor.
    private void Notify(ITaskStore store, TaskRun run) =>
        notifier.Publish(new RunChanged(new TaskRunRef(store.Schema, run.Id), run.ActingUserId));

    /// <summary>
    /// Sends the command in its own scope. <paramref name="progress"/> receives the last progress the command reported,
    /// read from the scope's <see cref="TaskProgress"/> before the scope is disposed, however the send ended (a throw
    /// included), so the caller can keep it on the item.
    /// </summary>
    private async Task<ErrorOr<Success>> SendAsync(
        string schema,
        TaskRun run,
        TaskRunItem item,
        TaskDescriptor? command,
        TaskTimeouts? timeouts,
        StrongBox<int?> progress,
        CancellationToken ct)
    {
        if (command is null)
        {
            return TaskErrors.UnknownCommand;
        }

        ErrorOr<Application.Commands.IBaseCommand> built = TaskCommandFactory.Build(command, run.Arguments, item.TargetId);

        if (built.IsError)
        {
            return built.Errors;
        }

        try
        {
            await using AsyncServiceScope scope = scopes.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<UserContextAccessor>().Current = new DelegatedUserContext(run.ActingUserId, run.Id);
            scope.ServiceProvider.GetRequiredService<IAuthorizationScopeContext>().Set(run.ScopeId);
            scope.ServiceProvider.GetRequiredService<RunItemContext>().Set(item.Id, schema, run.Id, run.ActingUserId, timeouts);

            try
            {
                object? response = await scope.ServiceProvider.GetRequiredService<ISender>().Send(built.Value, ct);

                return response is IErrorOr { IsError: true, Errors: [Error first, ..] } ? first : Result.Success;
            }
            finally
            {
                progress.Value = scope.ServiceProvider.GetRequiredService<TaskProgress>().Last;
            }
        }
        // A cancellation is the send's own only when its token caused it (the host stopping, a cancel, a timeout). One the
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
                    ITaskStore store = scope.ServiceProvider.GetRequiredService<ITaskStoreResolver>().For(schema);
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
