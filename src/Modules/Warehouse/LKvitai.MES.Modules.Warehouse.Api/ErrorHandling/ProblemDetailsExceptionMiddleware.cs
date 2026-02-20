using Microsoft.AspNetCore.Mvc;
using LKvitai.MES.Api.Security;

namespace LKvitai.MES.Api.ErrorHandling;

public sealed class ProblemDetailsExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ProblemDetailsExceptionMiddleware> _logger;

    public ProblemDetailsExceptionMiddleware(
        RequestDelegate next,
        ILogger<ProblemDetailsExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unhandled exception while processing request {Path}{Query}",
                context.Request.Path,
                SensitiveDataMasker.MaskText(context.Request.QueryString.Value));

            if (context.Response.HasStarted)
            {
                throw;
            }

            var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(ex, context);
            await WriteProblemDetailsAsync(context, problemDetails);
        }
    }

    private static Task WriteProblemDetailsAsync(HttpContext context, ProblemDetails problemDetails)
    {
        context.Response.Clear();
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/problem+json";
        return context.Response.WriteAsJsonAsync(problemDetails);
    }
}
