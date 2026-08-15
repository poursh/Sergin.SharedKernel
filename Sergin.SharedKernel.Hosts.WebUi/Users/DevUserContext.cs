using Sergin.SharedKernel.Application.Securities;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Hosts.WebUi.Users;

internal sealed record DevUserContext(
    UserId Id,
    string UserName,
    string Email,
    string FirstName,
    string LastName,
    HashSet<Permission> Permissions) : IUserContext;
