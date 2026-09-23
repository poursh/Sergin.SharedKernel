namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Aggregates;

/// <summary>
/// One per context added through AddModuleDbContext, so startup code can enumerate every module's
/// DbContext without knowing their types.
/// </summary>
public sealed record ModuleDbContextRegistration(Type ContextType);
