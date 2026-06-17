using System.Text.Json;
using LKvitai.MES.Modules.Shopfloor.Application.Exceptions;
using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;

namespace LKvitai.MES.Modules.Shopfloor.Api.Middleware;

/// <summary>
/// Maps Shopfloor application exceptions to HTTP status codes + a small JSON
/// problem body: 404 not found, 409 conflict, 400 validation. Unknown
/// exceptions bubble up to the default handler.
/// </summary>
public sealed class ShopfloorExceptionMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly RequestDelegate _next;
    private readonly ILogger<ShopfloorExceptionMiddleware> _logger;

    public ShopfloorExceptionMiddleware(RequestDelegate next, ILogger<ShopfloorExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context).ConfigureAwait(false);
        }
        catch (ShopfloorWorkflowNotPublishableException ex)
        {
            await WriteReportAsync(context, ex.Message, ex.Report).ConfigureAwait(false);
        }
        catch (ShopfloorValidationException ex)
        {
            await WriteAsync(context, StatusCodes.Status400BadRequest, ex.Message, ex.Errors).ConfigureAwait(false);
        }
        catch (ShopfloorNotFoundException ex)
        {
            await WriteAsync(context, StatusCodes.Status404NotFound, ex.Message, null).ConfigureAwait(false);
        }
        catch (ShopfloorConflictException ex)
        {
            await WriteAsync(context, StatusCodes.Status409Conflict, ex.Message, null).ConfigureAwait(false);
        }
    }

    private async Task WriteAsync(HttpContext context, int statusCode, string message, IReadOnlyList<string>? errors)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("[Shopfloor] cannot write error response, response already started for {Path}", context.Request.Path);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new
        {
            status = statusCode,
            message,
            errors = errors ?? Array.Empty<string>(),
        });

        await context.Response.WriteAsync(payload).ConfigureAwait(false);
    }

    private async Task WriteReportAsync(HttpContext context, string message, ValidationReportDto report)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning("[Shopfloor] cannot write 422 report, response already started for {Path}", context.Request.Path);
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = StatusCodes.Status422UnprocessableEntity;
        context.Response.ContentType = "application/json";

        var payload = JsonSerializer.Serialize(new
        {
            status = StatusCodes.Status422UnprocessableEntity,
            message,
            report,
        }, JsonOptions);

        await context.Response.WriteAsync(payload).ConfigureAwait(false);
    }
}
