using Microsoft.Extensions.Options;
using Sergin.SharedKernel.Domain.Securities;
using Sergin.SharedKernel.Application.Securities.Users;
using Sergin.SharedKernel.Domain.Users;

namespace Sergin.SharedKernel.Hosts.WebUi.Users;

internal sealed class ConfiguredUserContextFactory : IUserContextFactory
{
    private readonly IUserContext userContext;

    public ConfiguredUserContextFactory(IOptions<DevUserOptions> options)
    {
        DevUserOptions value = options.Value;

        userContext = new DevUserContext(
            new UserId(value.Id),
            value.UserName,
            value.Email,
            value.FirstName,
            value.LastName,
            [.. value.Permissions.Select(permission => (Permission)permission)]);
    }

    public IUserContext CreateUserContext() => userContext;
}
