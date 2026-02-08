using System.Diagnostics;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace LKvitai.MES.Api.ErrorHandling;

public static class ResultProblemDetailsMapper
{
    private static readonly IReadOnlyDictionary<string, int> StatusByErrorCode = new Dictionary<string, int>(StringComparer.Ordinal)
    {
        [DomainErrorCodes.ConcurrencyConflict] = StatusCodes.Status409Conflict,
        [DomainErrorCodes.IdempotencyInProgress] = StatusCodes.Status409Conflict,
        [DomainErrorCodes.IdempotencyAlreadyProcessed] = StatusCodes.Status409Conflict,
        [DomainErrorCodes.HardLockConflict] = StatusCodes.Status409Conflict,
        [DomainErrorCodes.InsufficientBalance] = StatusCodes.Status422UnprocessableEntity,
        [DomainErrorCodes.ReservationNotAllocated] = StatusCodes.Status400BadRequest,
        [DomainErrorCodes.InvalidProjectionName] = StatusCodes.Status400BadRequest,
        [DomainErrorCodes.ValidationError] = StatusCodes.Status400BadRequest,
        [DomainErrorCodes.NotFound] = StatusCodes.Status404NotFound,
        [DomainErrorCodes.Unauthorized] = StatusCodes.Status401Unauthorized,
        [DomainErrorCodes.Forbidden] = StatusCodes.Status403Forbidden,
        [DomainErrorCodes.InternalError] = StatusCodes.Status500InternalServerError
    };

    public static ProblemDetails ToProblemDetails(Result result, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(httpContext);

        var (errorCode, detail) = ResolveCodeAndDetail(result);
        return ToProblemDetails(errorCode, detail, httpContext);
    }

    public static ProblemDetails ToProblemDetails(Exception exception, HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentNullException.ThrowIfNull(httpContext);

        var (errorCode, detail) = exception switch
        {
            DomainException domainException => (NormalizeErrorCode(domainException.ErrorCode), domainException.Message),
            FluentValidation.ValidationException validationException => (DomainErrorCodes.ValidationError, validationException.Message),
            UnauthorizedAccessException unauthorizedException => (DomainErrorCodes.Unauthorized, unauthorizedException.Message),
            KeyNotFoundException notFoundException => (DomainErrorCodes.NotFound, notFoundException.Message),
            System.Security.SecurityException securityException => (DomainErrorCodes.Forbidden, securityException.Message),
            _ => (DomainErrorCodes.InternalError, "An unexpected error occurred.")
        };

        return ToProblemDetails(errorCode, detail, httpContext);
    }

    public static ProblemDetails ToProblemDetails(string errorCode, string detail, HttpContext httpContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);
        ArgumentNullException.ThrowIfNull(httpContext);

        var normalizedCode = NormalizeErrorCode(errorCode);
        var status = GetStatusCode(normalizedCode);
        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var problemDetails = new ProblemDetails
        {
            Status = status,
            Title = ReasonPhrases.GetReasonPhrase(status),
            Detail = string.IsNullOrWhiteSpace(detail)
                ? "The request failed."
                : detail
        };

        problemDetails.Extensions["errorCode"] = normalizedCode;
        problemDetails.Extensions["traceId"] = traceId;

        return problemDetails;
    }

    public static int GetStatusCode(string errorCode)
    {
        var normalizedCode = NormalizeErrorCode(errorCode);
        return StatusByErrorCode.TryGetValue(normalizedCode, out var status)
            ? status
            : StatusCodes.Status500InternalServerError;
    }

    private static (string ErrorCode, string Detail) ResolveCodeAndDetail(Result result)
    {
        if (!string.IsNullOrWhiteSpace(result.ErrorCode))
        {
            return (
                NormalizeErrorCode(result.ErrorCode),
                string.IsNullOrWhiteSpace(result.ErrorDetail) ? result.Error : result.ErrorDetail);
        }

        if (DomainErrorCodes.IsKnown(result.Error))
        {
            return (result.Error, result.Error);
        }

        return (
            DomainErrorCodes.InternalError,
            string.IsNullOrWhiteSpace(result.Error) ? "An unexpected error occurred." : result.Error);
    }

    private static string NormalizeErrorCode(string errorCode)
        => DomainErrorCodes.IsKnown(errorCode)
            ? errorCode
            : DomainErrorCodes.InternalError;
}
