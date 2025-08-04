using MediatR;

namespace Sergin.SharedKernel.Application.Commands;

public interface ICommandHandler<in TCommand> : ICommandHandler<TCommand, Success>
    where TCommand : ICommand;

public interface ICommandHandler<in TCommand, TResponse> : IRequestHandler<TCommand, ErrorOr<TResponse>>
    where TCommand : ICommand<TResponse>;

