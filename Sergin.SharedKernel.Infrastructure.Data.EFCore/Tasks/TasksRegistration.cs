namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Tasks;

/// <summary>One per context that opts into tasks (<see cref="ITasksDbContext"/>): its schema and type.</summary>
public sealed record TasksRegistration(string Schema, Type ContextType);
