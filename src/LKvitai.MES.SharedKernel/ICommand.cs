using MediatR;

namespace LKvitai.MES.SharedKernel;

/// <summary>
/// Base interface for all commands
/// </summary>
public interface ICommand : IRequest<Result>
{
    Guid CommandId { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
}

public interface ICommand<TResult> : IRequest<Result<TResult>>
{
    Guid CommandId { get; }
    Guid CorrelationId { get; }
    Guid CausationId { get; }
}
