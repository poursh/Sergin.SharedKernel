namespace Sergin.SharedKernel.Hosts.WebUi.Users;

public sealed class DevUserOptions
{
    public const string SectionName = "DevUser";

    public Guid Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public bool Validate(out string failure)
    {
        if (Id == Guid.Empty)
        {
            failure = $"Sergin:{SectionName}:Id must be a non-empty GUID.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(UserName))
        {
            failure = $"Sergin:{SectionName}:UserName is required.";
            return false;
        }

        failure = string.Empty;
        return true;
    }
}
