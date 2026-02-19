namespace LKvitai.MES.SharedKernel;

/// <summary>
/// Base interface for all commands
/// </summary>
public interface ICommand : LKvitai.MES.BuildingBlocks.Cqrs.Abstractions.ICommand<Result>
{
}

public interface ICommand<TResult> : LKvitai.MES.BuildingBlocks.Cqrs.Abstractions.ICommand<Result<TResult>>
{
}
