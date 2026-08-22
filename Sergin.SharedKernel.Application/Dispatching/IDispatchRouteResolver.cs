namespace Sergin.SharedKernel.Application.Dispatching;

public interface IDispatchRouteResolver
{
    bool IsRemote(Type requestType);
}
