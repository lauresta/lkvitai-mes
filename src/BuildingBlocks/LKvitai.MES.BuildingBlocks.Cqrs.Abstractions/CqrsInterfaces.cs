namespace LKvitai.MES.BuildingBlocks.Cqrs.Abstractions;

public interface ICommand
{
    Guid CommandId { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
}

public interface ICommand<out TResult> : ICommand
{
}

public interface IQuery<out TResult>
{
}

public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task Handle(TCommand command, CancellationToken cancellationToken);
}

public interface IQueryHandler<in TQuery, TResult>
    where TQuery : IQuery<TResult>
{
    Task<TResult> Handle(TQuery query, CancellationToken cancellationToken);
}
