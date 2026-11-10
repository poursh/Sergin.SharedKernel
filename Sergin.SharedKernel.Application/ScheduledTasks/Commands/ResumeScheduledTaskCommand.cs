using Microsoft.Extensions.Logging;
using Sergin.SharedKernel.Application.Commands;
using Sergin.SharedKernel.Application.Commands.Types;
using Sergin.SharedKernel.Domain.ScheduledTasks;

namespace Sergin.SharedKernel.Application.ScheduledTasks.Commands;

/// <summary>Lets a paused schedule fire again. Version-guarded by its type.</summary>
public sealed record ResumeScheduledTaskCommand(string Schema, ScheduledTaskId Id)
    : ICommand, ITransitionCommand<ScheduledTask, ScheduledTaskId>, IReverseOf<PauseScheduledTaskCommand>;

internal sealed class ResumeScheduledTaskCommandHandler(IScheduledTaskStoreResolver stores, ScheduledTaskEngineAccessor engines, ILogger<ResumeScheduledTaskCommandHandler> logger)
    : ICommandHandler<ResumeScheduledTaskCommand>
{
    public Task<ErrorOr<Success>> Handle(ResumeScheduledTaskCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return ScheduledTaskStateChange.ApplyAsync(stores.For(request.Schema), request.Id, enabled: true, engines.Engine, logger, cancellationToken);
    }
}
