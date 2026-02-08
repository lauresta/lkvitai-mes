using LKvitai.MES.Application.Ports;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Application.Behaviors;

/// <summary>
/// MediatR pipeline behavior that enforces command idempotency via atomic claim.
///
/// Flow:
///   1. TryStartAsync → atomic INSERT-based claim
///      - Started          → execute handler → CompleteAsync / FailAsync
///      - AlreadyCompleted → short-circuit OK (no handler invocation)
///      - InProgress       → return IDEMPOTENCY_IN_PROGRESS error
///   2. On handler success  → CompleteAsync
///   3. On handler failure  → FailAsync (allows future retry)
///
/// Command-agnostic: uses generic IDEMPOTENCY_* error codes.
/// Persisted via <see cref="IProcessedCommandStore"/> (Marten doc store).
/// </summary>
public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand
    where TResponse : Result
{
    private readonly IProcessedCommandStore _store;
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;

    public IdempotencyBehavior(
        IProcessedCommandStore store,
        ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var commandId = request.CommandId;
        var commandType = typeof(TRequest).Name;

        // ── Atomic claim ───────────────────────────────────────────────
        var claim = await _store.TryStartAsync(commandId, commandType, cancellationToken);

        switch (claim)
        {
            case CommandClaimResult.AlreadyCompleted:
                _logger.LogInformation(
                    "Command {CommandId} ({CommandType}) already completed; returning cached OK",
                    commandId, commandType);
                return (TResponse)(object)Result.Ok();

            case CommandClaimResult.InProgress:
                _logger.LogWarning(
                    "Command {CommandId} ({CommandType}) is already in progress by another instance",
                    commandId, commandType);
                return (TResponse)(object)Result.Fail(DomainErrorCodes.IdempotencyInProgress);

            case CommandClaimResult.Started:
                // Claim acquired — proceed to handler
                break;

            default:
                _logger.LogError("Unexpected claim result {Claim} for command {CommandId}", claim, commandId);
                return (TResponse)(object)Result.Fail(DomainErrorCodes.IdempotencyInProgress);
        }

        // ── Execute handler ────────────────────────────────────────────
        try
        {
            var result = await next();

            if (result.IsSuccess)
            {
                await _store.CompleteAsync(commandId, cancellationToken);
            }
            else
            {
                await _store.FailAsync(commandId, result.Error, cancellationToken);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Command {CommandId} ({CommandType}) threw an exception",
                commandId, commandType);

            try
            {
                await _store.FailAsync(commandId, ex.GetType().Name, cancellationToken);
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "Failed to persist FAILED status for command {CommandId}",
                    commandId);
            }

            throw;
        }
    }
}
