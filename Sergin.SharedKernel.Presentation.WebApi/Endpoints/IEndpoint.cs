using Microsoft.AspNetCore.Routing;

namespace Sergin.SharedKernel.Presentation.WebApi.Endpoints;

public interface IEndpoint
{
    void MapEndpoint(IEndpointRouteBuilder routeBuilder);
}
