namespace Sergin.SharedKernel.Application.Aggregates;

/// <summary>
/// The audit stamps of one record of an <c>Audited()</c> aggregate, as a read model: who created it and when, and
/// who last changed it and when — <see langword="null"/> until its first update. The user ids are Sergin user ids;
/// <see cref="Securities.Users.IUserDirectory"/> turns them into names for display.
/// </summary>
public sealed record AuditInfo(DateTime CreatedAtUtc, Guid CreatedBy, DateTime? ModifiedAtUtc, Guid? ModifiedBy);
