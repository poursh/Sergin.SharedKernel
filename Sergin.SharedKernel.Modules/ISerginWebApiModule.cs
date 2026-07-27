using Microsoft.AspNetCore.Routing;

namespace Sergin.SharedKernel.Modules;

public interface ISerginWebApiModule : ISerginModule
{
    void MapEndpoints(RouteGroupBuilder group);
}
