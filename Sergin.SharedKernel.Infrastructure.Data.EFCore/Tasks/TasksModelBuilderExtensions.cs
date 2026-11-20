using Microsoft.EntityFrameworkCore;
using Sergin.SharedKernel.Domain.Tasks;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Tasks;

/// <summary>
/// Maps the scheduled-task tables into the model's default schema. A module calls it by hand from its own
/// <c>OnModelCreating</c>, like <c>ApplyOutbox</c>; see <see cref="ITasksDbContext"/> for the rest of the opt-in.
/// The task's audit, soft-delete, version and scope columns come from its aggregate feature configuration; runs and
/// run items are mapped here in full and take no features. Enums are stored as their names.
/// </summary>
public static class TasksModelBuilderExtensions
{
    private const string JsonbColumnType = "jsonb";

    /// <summary>The partial unique index that lets only one run of a task be Running: the overlap rule's guarantee.</summary>
    internal const string OneRunningIndexName = "ix_task_runs_schedule_id_running";

    /// <summary>
    /// The partial unique index that lets only one run of a collection task be Running per task name and scope: decision
    /// 10's guarantee. The start's check before the insert is advisory.
    /// </summary>
    internal const string CollectionRunningIndexName = "ix_task_runs_collection_running";

    internal const string StaleItemsIndexName = "ix_task_run_items_unfinished_last_activity";

    public static ModelBuilder ApplyTasks(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<TaskSchedule>(task =>
        {
            task.ToTable("task_schedules");
            task.HasKey(x => x.Id);
            task.Property(x => x.Id).HasConversion(id => id.Value, value => new TaskScheduleId(value));
            task.Property(x => x.CommandName).IsRequired();
            task.Property(x => x.Arguments).IsRequired().HasColumnType(JsonbColumnType);
            task.Property(x => x.Cron).IsRequired();
            task.Property(x => x.TimeZoneId).IsRequired();
            task.Property(x => x.State).IsRequired().HasConversion<string>();
            task.Property(x => x.DelegatingUserId).IsRequired().HasConversion(id => id.Value, value => new UserId(value));

            task.OwnsMany(x => x.Targets, target =>
            {
                target.ToTable("task_schedule_targets");
                target.WithOwner().HasForeignKey("ScheduleId");
                target.HasKey("ScheduleId", nameof(TaskScheduleTarget.TargetId));

                // A record's page finds the tasks that target it.
                target.HasIndex(x => x.TargetId);
            });
            task.Navigation(x => x.Targets).HasField("targets");
        });

        modelBuilder.Entity<TaskRun>(run =>
        {
            run.ToTable("task_runs");
            run.HasKey(x => x.Id);
            run.Property(x => x.ScheduleId).HasConversion(id => id!.Value, value => new TaskScheduleId(value));
            run.Property(x => x.Trigger).IsRequired().HasConversion<string>();
            run.Property(x => x.ActingUserId).IsRequired().HasConversion(id => id.Value, value => new UserId(value));
            run.Property(x => x.ScopePath).IsRequired().HasColumnType("ltree");
            run.Property(x => x.CommandName).IsRequired();
            run.Property(x => x.Arguments).IsRequired().HasColumnType(JsonbColumnType);
            run.Property(x => x.Status).IsRequired().HasConversion<string>();
            run.Property(x => x.TargetKind).IsRequired().HasConversion<string>();

            // Zero for every run that predates cancelling, and for every run until it completes.
            run.Property(x => x.Cancelled).HasDefaultValue(0);

            // A deleted task is soft-deleted and keeps its history, so nothing cascades from it.
            run.HasOne<TaskSchedule>().WithMany().HasForeignKey(x => x.ScheduleId).OnDelete(DeleteBehavior.Restrict);

            // HasUnfinishedRunAsync is check-then-act and races; this is the guarantee. One-off runs (no task) and Skipped
            // or Completed runs fall outside the filter. Statuses are stored as their names.
            run.HasIndex(x => x.ScheduleId).IsUnique().HasDatabaseName(OneRunningIndexName).HasFilter("status = 'Running'");

            // A collection task runs once at a time in a scope, whatever started it. Entity runs fall outside the filter.
            run.HasIndex(x => new { x.CommandName, x.ScopeId }).IsUnique().HasDatabaseName(CollectionRunningIndexName)
                .HasFilter("status = 'Running' AND target_kind = 'Collection'");

            // A task's history, newest first.
            run.HasIndex(x => new { x.ScheduleId, x.StartedAtUtc }).IsDescending(false, true);

            // The app bar's activity list: a caller's runs, newest first.
            run.HasIndex(x => new { x.ActingUserId, x.StartedAtUtc }).IsDescending(false, true);
        });

        modelBuilder.Entity<TaskRunItem>(item =>
        {
            item.ToTable("task_run_items");
            item.HasKey(x => x.Id);
            item.Property(x => x.Status).IsRequired().HasConversion<string>();

            // A percent, 0 to 100: a smallint is enough.
            item.Property(x => x.Progress).HasConversion<short>().HasColumnType("smallint");
            item.HasOne<TaskRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);

            // The recovery sweep's lookup: unfinished items quiet since before a cutoff.
            item.HasIndex(x => new { x.Status, x.LastActivityAtUtc }).HasDatabaseName(StaleItemsIndexName)
                .HasFilter("status IN ('Pending', 'Running', 'Retrying')");

            // A record's one-off runs (the schedules section): the items over a target, each naming its run.
            item.HasIndex(x => new { x.TargetId, x.RunId });
        });

        return modelBuilder;
    }
}
