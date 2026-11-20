using Microsoft.Extensions.DependencyInjection;

namespace Sergin.SharedKernel.Application.Tasks.Commands;

/// <summary>
/// The engine, resolved when a management command needs it rather than injected: the engine is the adapter's to
/// register, and a host that opts a module in without one (a model test host) must still pass the container's
/// build-time validation, as <see cref="TaskRunStarter"/> does.
/// </summary>
internal sealed class TaskEngineAccessor(IServiceProvider services)
{
    public ITaskEngine Engine => services.GetRequiredService<ITaskEngine>();
}
