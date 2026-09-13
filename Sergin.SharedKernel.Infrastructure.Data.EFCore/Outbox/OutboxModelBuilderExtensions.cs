using Microsoft.EntityFrameworkCore;

namespace Sergin.SharedKernel.Infrastructure.Data.EFCore.Outbox;

/// <summary>
/// Maps <see cref="OutboxMessage"/> and <see cref="InboxMessage"/> into the model's default schema. A module
/// calls this by hand, from its own <c>OnModelCreating</c>, after its own entity configurations — there is no
/// unconditional mapping in <c>SerginDbContext</c> itself, because EF Core raises a
/// <c>PendingModelChangesWarning</c> at startup for any module (test contexts included) that has not yet
/// migrated the tables in.
/// </summary>
public static class OutboxModelBuilderExtensions
{
    public static ModelBuilder ApplyOutbox(this ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.ToTable("outbox_messages");
            outbox.HasKey(x => x.Id);
            outbox.Property(x => x.Type).IsRequired();
            outbox.Property(x => x.Content).IsRequired().HasColumnType("jsonb");
            outbox.Property(x => x.CorrelationId).IsRequired();
            outbox.Property(x => x.Error).HasMaxLength(OutboxMessage.MaxErrorLength);

            // Partial index: the relay only ever scans unprocessed rows, so indexing just those keeps the
            // index small regardless of how many processed rows Retention allows to accumulate between purges.
            outbox.HasIndex(x => x.Id)
                .HasDatabaseName("ix_outbox_messages_unprocessed")
                .HasFilter("processed_on_utc IS NULL");
        });

        modelBuilder.Entity<InboxMessage>(inbox =>
        {
            inbox.ToTable("inbox_messages");
            inbox.HasKey(x => new { x.MessageId, x.Handler });
        });

        return modelBuilder;
    }
}
