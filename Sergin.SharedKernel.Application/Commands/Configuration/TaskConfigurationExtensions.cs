using Sergin.SharedKernel.Application.Commands.Types;

namespace Sergin.SharedKernel.Application.Commands.Configuration;

/// <summary><c>Task(...)</c>, offered only on a task's configuration: calling it on any other request does not compile.</summary>
public static class TaskConfigurationExtensions
{
    /// <summary>
    /// Declares how the task runs (<see cref="TaskBuilder"/>). Calling it again adds to what an earlier call declared.
    /// </summary>
    public static CommandConfigurationBuilder<TCommand> Task<TCommand>(
        this CommandConfigurationBuilder<TCommand> builder, Action<TaskBuilder> configure)
        where TCommand : ITaskCommand
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        configure(builder.TaskDeclaration);
        return builder;
    }
}
