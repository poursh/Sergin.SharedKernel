using Microsoft.EntityFrameworkCore;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

/// <summary>
/// Opt-in marker a module's <c>DbContext</c> implements to carry the four scheduled-task tables in its own schema,
/// the way <c>IOutboxDbContext</c> opts into the outbox. An opting-in context must also:
/// <list type="bullet">
/// <item>call <c>modelBuilder.ApplyScheduledTasks()</c> from its <c>OnModelCreating</c>;</item>
/// <item>include <c>ScheduledTaskAggregateFeatureConfiguration</c> in its <c>AggregateFeatures</c>
/// (<c>AggregateFeatureRegistry.FromConfigurationTypes([.. its own, .. ScheduledTaskConfigurationTypes.AggregateConfigurations])</c>),
/// or the task gets no audit, soft-delete, version or scope columns and the startup guard refuses the host.</item>
/// </list>
/// <c>AddModuleDbContext</c> sees the interface and registers a <see cref="ScheduledTasksRegistration"/> for the
/// context, plus, once per host, the scheduled-task configurations as a configuration source; the host's aggregate
/// feature registry then knows the task too. A context that does not implement it gets no tables and no cost.
/// </summary>
public interface IScheduledTasksDbContext
{
    DbSet<ScheduledTask> ScheduledTasks { get; }

    DbSet<ScheduledTaskRun> ScheduledTaskRuns { get; }

    DbSet<ScheduledTaskRunItem> ScheduledTaskRunItems { get; }
}
