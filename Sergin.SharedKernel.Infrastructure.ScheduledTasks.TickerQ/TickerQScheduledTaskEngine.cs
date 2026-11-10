using System.Text.Json;
using Sergin.SharedKernel.Application.ScheduledTasks;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using TickerQ.Utilities;
using TickerQ.Utilities.Entities;
using TickerQ.Utilities.Enums;
using TickerQ.Utilities.Interfaces.Managers;
using TickerQ.Utilities.Models;

namespace Sergin.SharedKernel.Infrastructure.ScheduledTasks.TickerQ;

/// <summary>
/// <see cref="IScheduledTaskEngine"/> over TickerQ. A task's trigger is a cron ticker of
/// <see cref="SerginTickerFunctions.Fire"/>; a run item is a time ticker of <see cref="SerginTickerFunctions.Execute"/>
/// due now. TickerQ's own retries are off for both (retry and overlap are Sergin's), and a run item's job restarts
/// when the node executing it stops renewing its lease (<see cref="StaleAction.Restart"/>). Every manager call's result
/// is checked: a refusal throws <see cref="InvalidOperationException"/> with TickerQ's exception inside, which the
/// management handlers report as the engine being unavailable.
/// </summary>
public sealed class TickerQScheduledTaskEngine(
    ICronTickerManager<CronTickerEntity> crons,
    ITimeTickerManager<TimeTickerEntity> jobs) : IScheduledTaskEngine
{
    private static readonly JsonSerializerOptions ReadOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<Guid> ScheduleAsync(ScheduledTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        var cron = new CronTickerEntity
        {
            Id = Guid.CreateVersion7(),
            Function = SerginTickerFunctions.Fire,
            Expression = trigger.Cron,
            TimeZoneId = trigger.TimeZoneId,
            Request = TriggerRequest(trigger),
            Description = Describe(trigger),
            Retries = 0,
            SkipIfAlreadyRunning = false,
        };

        Ensure(await crons.AddAsync(cron, ct), "schedule", cron.Id);

        return cron.Id;
    }

    /// <summary>
    /// Changes the loaded ticker's expression, zone and payload and writes it back whole, so its enabled state is the
    /// one it had: a paused task's trigger stays disabled.
    /// </summary>
    public async Task RescheduleAsync(Guid engineTriggerId, ScheduledTrigger trigger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trigger);

        CronTickerEntity cron = await crons.GetAsync(engineTriggerId, ct)
            ?? throw new InvalidOperationException($"TickerQ holds no cron ticker {engineTriggerId} to reschedule.");

        cron.Expression = trigger.Cron;
        cron.TimeZoneId = trigger.TimeZoneId;
        cron.Request = TriggerRequest(trigger);
        cron.Description = Describe(trigger);

        TickerResult<CronTickerEntity> result = await crons.UpdateAsync(cron, ct);
        Ensure(result, "reschedule", engineTriggerId);

        if (result.AffectedRows <= 0)
        {
            throw new InvalidOperationException($"TickerQ changed nothing when rescheduling cron ticker {engineTriggerId}: it is gone.");
        }
    }

    public async Task SetEnabledAsync(Guid engineTriggerId, bool enabled, CancellationToken ct) =>
        Ensure(await crons.SetEnabledAsync(engineTriggerId, enabled, ct), enabled ? "enable" : "disable", engineTriggerId);

    /// <summary>Removing a trigger the engine no longer holds is not a failure: the trigger is gone either way.</summary>
    public async Task RemoveAsync(Guid engineTriggerId, CancellationToken ct) =>
        Ensure(await crons.DeleteAsync(engineTriggerId, ct), "remove", engineTriggerId);

    public async Task<bool> ExistsAsync(Guid engineTriggerId, CancellationToken ct) =>
        await crons.GetAsync(engineTriggerId, ct) is not null;

    public async Task<bool?> GetEnabledAsync(Guid engineTriggerId, CancellationToken ct) =>
        (await crons.GetAsync(engineTriggerId, ct))?.IsEnabled;

    /// <summary>Sergin's triggers are the cron tickers of <see cref="SerginTickerFunctions.Fire"/>; one whose payload does not read is skipped.</summary>
    public async Task<IReadOnlyCollection<ScheduledEngineTrigger>> ListTriggersAsync(CancellationToken ct)
    {
        IReadOnlyList<CronTickerEntity> all = await crons.ListAsync(cron => cron.Function == SerginTickerFunctions.Fire, ct);
        List<ScheduledEngineTrigger> triggers = [];

        foreach (CronTickerEntity cron in all)
        {
            if (ReadTriggerRequest(cron.Request) is { } request)
            {
                triggers.Add(new ScheduledEngineTrigger(cron.Id, request.Schema, new ScheduledTaskId(request.TaskId), cron.CreatedAt));
            }
        }

        return triggers;
    }

    public Task<Guid> EnqueueRunItemAsync(ScheduledRunItemReference item, CancellationToken ct) => EnqueueRunItemAsync(item, null, ct);

    /// <summary>
    /// What TickerQ itself can fire: its own cron parser (five or six fields) and a zone the system knows. A
    /// <c>%placeholder%</c>, which TickerQ resolves from configuration, is not a schedule a person typed and is refused.
    /// </summary>
    public bool IsValidCron(string cron, string timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(cron) || cron.Contains('%', StringComparison.Ordinal) || string.IsNullOrWhiteSpace(timeZoneId))
        {
            return false;
        }

        if (!CronExpression.TryParse(cron, out _))
        {
            return false;
        }

        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return true;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return false;
        }
    }

    /// <summary>A run item's job, due at <paramref name="executionTimeUtc"/> or, when null, now.</summary>
    internal async Task<Guid> EnqueueRunItemAsync(ScheduledRunItemReference item, DateTime? executionTimeUtc, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        var job = new TimeTickerEntity
        {
            Id = Guid.CreateVersion7(),
            Function = SerginTickerFunctions.Execute,
            Request = TickerHelper.CreateTickerRequest(item, SerginTickerFunctions.Execute),
            Description = $"{item.Schema} run item {item.RunItemId}",
            ExecutionTime = executionTimeUtc,
            Retries = 0,
            OnStale = StaleAction.Restart,
        };

        Ensure(await jobs.AddAsync(job, ct), "enqueue the run item of", job.Id);

        return job.Id;
    }

    private static byte[] TriggerRequest(ScheduledTrigger trigger) =>
        TickerHelper.CreateTickerRequest(new ScheduledTriggerRequest(trigger.Schema, trigger.TaskId.Value), SerginTickerFunctions.Fire);

    private static ScheduledTriggerRequest? ReadTriggerRequest(byte[]? request)
    {
        if (request is not { Length: > 0 })
        {
            return null;
        }

        // Read as text, then bound by name whatever its casing: the function's source-generated JSON metadata exists only
        // once TickerQ is activated, and a listing must work before that too.
        try
        {
            string json = TickerHelper.ReadTickerRequestAsString(request);

            return string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<ScheduledTriggerRequest>(json, ReadOptions);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException or NotSupportedException)
        {
            return null;
        }
    }

    private static string Describe(ScheduledTrigger trigger) => $"{trigger.Schema} scheduled task {trigger.TaskId.Value}";

    private static void Ensure<TEntity>(TickerResult<TEntity> result, string action, Guid id)
        where TEntity : class
    {
        if (!result.IsSucceeded)
        {
            throw new InvalidOperationException($"TickerQ refused to {action} ticker {id}.", result.Exception);
        }
    }
}
