using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Application.Behaviors;

/// <summary>
/// Command idempotency behavior per blueprint
/// Checks if command already processed and returns cached result
/// </summary>
public class IdempotencyBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand
    where TResponse : Result
{
    private readonly ILogger<IdempotencyBehavior<TRequest, TResponse>> _logger;
    
    public IdempotencyBehavior(ILogger<IdempotencyBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Implementation per blueprint to be added
        // Check processed_commands table, return cached result if exists
        // Otherwise execute and store result
        
        _logger.LogInformation(
            "Processing command {CommandType} with ID {CommandId}",
            typeof(TRequest).Name,
            request.CommandId);
        
        return await next();
    }
}
