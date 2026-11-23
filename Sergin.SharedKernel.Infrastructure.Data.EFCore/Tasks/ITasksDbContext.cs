using Microsoft.EntityFrameworkCore;
using Sergin.SharedKernel.Domain.Tasks;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Tasks;

/// <summary>
/// Opt-in marker a module's <c>DbContext</c> implements to carry the four task tables in its own schema,
/// the way <c>IOutboxDbContext</c> opts into the outbox. An opting-in context must also:
/// <list type="bullet">
/// <item>call <c>modelBuilder.ApplyTasks()</c> from its <c>OnModelCreating</c>;</item>
/// <item>include <c>TaskScheduleAggregateFeatureConfiguration</c> in its <c>AggregateFeatures</c>
/// (<c>AggregateFeatureRegistry.FromConfigurationTypes([.. its own, .. TaskConfigurationTypes.AggregateConfigurations])</c>),
/// or the task gets no audit, soft-delete, version or scope columns and the startup guard refuses the host.</item>
/// </list>
/// <c>AddModuleDbContext</c> sees the interface and registers a <see cref="TasksRegistration"/> for the
/// context, plus, once per host, the task configurations as a configuration source; the host's aggregate
/// feature registry then knows the task too. A context that does not implement it gets no tables and no cost.
/// </summary>
public interface ITasksDbContext
{
    DbSet<TaskSchedule> TaskSchedules { get; }

    DbSet<TaskRun> TaskRuns { get; }

    DbSet<TaskRunItem> TaskRunItems { get; }
}
