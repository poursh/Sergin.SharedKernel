namespace Sergin.SharedKernel.Presentation.Blazor.Dispatching;

public interface IDispatchRouteResolver
{
    bool IsRemote(Type requestType);
}
