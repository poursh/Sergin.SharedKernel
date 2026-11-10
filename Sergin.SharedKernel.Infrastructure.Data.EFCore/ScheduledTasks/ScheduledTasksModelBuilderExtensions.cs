using Microsoft.EntityFrameworkCore;
using Sergin.SharedKernel.Domain.ScheduledTasks;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

/// <summary>
/// Maps the scheduled-task tables into the model's default schema. A module calls it by hand from its own
/// <c>OnModelCreating</c>, like <c>ApplyOutbox</c>; see <see cref="IScheduledTasksDbContext"/> for the rest of the opt-in.
/// The task's audit, soft-delete, version and scope columns come from its aggregate feature configuration; runs and
/// run items are mapped here in full and take no features. Enums are stored as their names.
/// </summary>
public static class ScheduledTasksModelBuilderExtensions
{
    private const string JsonbColumnType = "jsonb";

    /// <summary>The partial unique index that lets only one run of a task be Running: the overlap rule's guarantee.</summary>
    internal const string OneRunningIndexName = "ix_scheduled_task_runs_task_id_running";

    public static ModelBuilder ApplyScheduledTasks(this ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.Entity<ScheduledTask>(task =>
        {
            task.ToTable("scheduled_tasks");
            task.HasKey(x => x.Id);
            task.Property(x => x.Id).HasConversion(id => id.Value, value => new ScheduledTaskId(value));
            task.Property(x => x.CommandName).IsRequired();
            task.Property(x => x.Arguments).IsRequired().HasColumnType(JsonbColumnType);
            task.Property(x => x.Cron).IsRequired();
            task.Property(x => x.TimeZoneId).IsRequired();
            task.Property(x => x.State).IsRequired().HasConversion<string>();
            task.Property(x => x.DelegatingUserId).IsRequired().HasConversion(id => id.Value, value => new UserId(value));

            task.OwnsMany(x => x.Targets, target =>
            {
                target.ToTable("scheduled_task_targets");
                target.WithOwner().HasForeignKey("TaskId");
                target.HasKey("TaskId", nameof(ScheduledTaskTarget.TargetId));

                // A record's page finds the tasks that target it.
                target.HasIndex(x => x.TargetId);
            });
            task.Navigation(x => x.Targets).HasField("targets");
        });

        modelBuilder.Entity<ScheduledTaskRun>(run =>
        {
            run.ToTable("scheduled_task_runs");
            run.HasKey(x => x.Id);
            run.Property(x => x.TaskId).HasConversion(id => id!.Value, value => new ScheduledTaskId(value));
            run.Property(x => x.Trigger).IsRequired().HasConversion<string>();
            run.Property(x => x.ActingUserId).IsRequired().HasConversion(id => id.Value, value => new UserId(value));
            run.Property(x => x.ScopePath).IsRequired().HasColumnType("ltree");
            run.Property(x => x.CommandName).IsRequired();
            run.Property(x => x.Arguments).IsRequired().HasColumnType(JsonbColumnType);
            run.Property(x => x.Status).IsRequired().HasConversion<string>();

            // A deleted task is soft-deleted and keeps its history, so nothing cascades from it.
            run.HasOne<ScheduledTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);

            // HasUnfinishedRunAsync is check-then-act and races; this is the guarantee. One-off runs (no task) and Skipped
            // or Completed runs fall outside the filter. Statuses are stored as their names.
            run.HasIndex(x => x.TaskId).IsUnique().HasDatabaseName(OneRunningIndexName).HasFilter("status = 'Running'");

            // A task's history, newest first.
            run.HasIndex(x => new { x.TaskId, x.StartedAtUtc }).IsDescending(false, true);
        });

        modelBuilder.Entity<ScheduledTaskRunItem>(item =>
        {
            item.ToTable("scheduled_task_run_items");
            item.HasKey(x => x.Id);
            item.Property(x => x.Status).IsRequired().HasConversion<string>();
            item.HasOne<ScheduledTaskRun>().WithMany().HasForeignKey(x => x.RunId).OnDelete(DeleteBehavior.Cascade);
        });

        return modelBuilder;
    }
}
