using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LKvitai.MES.Application.Behaviors;

/// <summary>
/// Logging behavior for command pipeline
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand
    where TResponse : Result
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;
    
    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger;
    }
    
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var commandName = typeof(TRequest).Name;
        var stopwatch = Stopwatch.StartNew();
        
        _logger.LogInformation(
            "Executing command {CommandName} with ID {CommandId}",
            commandName,
            request.CommandId);
        
        try
        {
            var response = await next();
            
            stopwatch.Stop();
            
            if (response.IsSuccess)
            {
                _logger.LogInformation(
                    "Command {CommandName} completed successfully in {ElapsedMs}ms",
                    commandName,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Command {CommandName} failed: {Error}",
                    commandName,
                    response.Error);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            
            _logger.LogError(ex,
                "Command {CommandName} threw exception after {ElapsedMs}ms",
                commandName,
                stopwatch.ElapsedMilliseconds);
            
            throw;
        }
    }
}
