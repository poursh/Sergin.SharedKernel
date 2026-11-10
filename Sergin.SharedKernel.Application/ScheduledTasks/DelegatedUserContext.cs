namespace Sergin.SharedKernel.Application.ScheduledTasks;

/// <summary>
/// The run's acting user by id only. The names are fixed placeholders: audit stamps and grant checks read the id,
/// and display names come from the user directory, not from the context.
/// </summary>
internal sealed record DelegatedUserContext(UserId Id, Guid RunId) : IDelegatedUserContext
{
    public string UserName => "scheduled-run";

    public string FirstName => "Scheduled";

    public string LastName => "Run";

    public string Email => "scheduled-run@sergin.local";
}
