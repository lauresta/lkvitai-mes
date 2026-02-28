using LKvitai.MES.BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace LKvitai.MES.Modules.Warehouse.Application.Behaviors;

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
            "Command started: {CommandName}. CommandId={CommandId}, CorrelationId={CorrelationId}, CausationId={CausationId}",
            commandName,
            request.CommandId,
            request.CorrelationId,
            request.CausationId);

        try
        {
            var response = await next();

            stopwatch.Stop();

            if (response.IsSuccess)
            {
                _logger.LogInformation(
                    "Command succeeded: {CommandName}. CommandId={CommandId}, ElapsedMs={ElapsedMs}",
                    commandName,
                    request.CommandId,
                    stopwatch.ElapsedMilliseconds);
            }
            else
            {
                _logger.LogWarning(
                    "Command failed: {CommandName}. CommandId={CommandId}, ErrorCode={ErrorCode}, Error={Error}, ErrorDetail={ErrorDetail}, ElapsedMs={ElapsedMs}",
                    commandName,
                    request.CommandId,
                    response.ErrorCode,
                    response.Error,
                    response.ErrorDetail,
                    stopwatch.ElapsedMilliseconds);
            }

            return response;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(ex,
                "Command threw exception: {CommandName}. CommandId={CommandId}, ElapsedMs={ElapsedMs}",
                commandName,
                request.CommandId,
                stopwatch.ElapsedMilliseconds);

            throw;
        }
    }
}
