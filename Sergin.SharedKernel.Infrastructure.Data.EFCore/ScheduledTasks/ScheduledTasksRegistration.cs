namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.ScheduledTasks;

/// <summary>One per context that opts into scheduled tasks (<see cref="IScheduledTasksDbContext"/>): its schema and type.</summary>
public sealed record ScheduledTasksRegistration(string Schema, Type ContextType);
