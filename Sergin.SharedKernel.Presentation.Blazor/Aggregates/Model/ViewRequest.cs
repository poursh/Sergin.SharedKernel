namespace Sergin.SharedKernel.Presentation.Blazor.Aggregates;

/// <summary>
/// One request a generated view sends: its type, the invoker that sends it and the delegate that builds it.
/// </summary>
/// <typeparam name="TBuild">The shape of the build delegate, which differs by request (from an id, a page's paging, form values, nothing).</typeparam>
/// <param name="Type">The request type.</param>
/// <param name="Invoker">Sends it through the dispatcher.</param>
/// <param name="Build">Builds it from what the view has.</param>
public sealed record ViewRequest<TBuild>(Type Type, RequestInvoker Invoker, TBuild Build)
    where TBuild : Delegate;
