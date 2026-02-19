using MediatR;

namespace LKvitai.MES.BuildingBlocks.Cqrs.Abstractions;

public interface ICommand
{
    Guid CommandId { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
}

public interface ICommand<out TResult> : IRequest<TResult>, ICommand
{
}

public interface IQuery<out TResult> : IRequest<TResult>
{
}

public interface ICommandHandler<in TCommand> : IRequestHandler<TCommand, Unit>
    where TCommand : ICommand<Unit>
{
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}
