using Microsoft.EntityFrameworkCore;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// Opt-in marker a module's <c>DbContext</c> implements to carry the outbox and inbox tables alongside its
/// own aggregates. <c>ModuleDbContextExtensions.AddModuleDbContext</c> checks for this interface to decide
/// whether to also register an inbox for the module, and <c>OutboxWriter</c> checks for it only once a
/// domain event actually needs translating — so a module that never raises a translated event never has to
/// implement it, or carry the tables its context would otherwise pay for.
/// </summary>
public interface IOutboxDbContext
{
    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<InboxMessage> InboxMessages { get; }
}
