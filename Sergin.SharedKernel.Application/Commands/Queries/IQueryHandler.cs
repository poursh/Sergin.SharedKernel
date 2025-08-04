using ErrorOr;
using MediatR;

namespace Sergin.SharedKernel.Application.Commands.Queries;

public interface IQueryHandler<in TQuery, TResponse> : IRequestHandler<TQuery, ErrorOr<TResponse>>
    where TQuery : IQuery<TResponse>;
