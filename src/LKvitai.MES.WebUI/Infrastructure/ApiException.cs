namespace LKvitai.MES.WebUI.Infrastructure;

public class ApiException : Exception
{
    public int StatusCode { get; }
    public string? ErrorCode { get; }
    public string? TraceId { get; }
    public string UserMessage { get; }
    public ProblemDetailsModel? ProblemDetails { get; }

    public ApiException(ProblemDetailsModel? problemDetails, int statusCode)
        : base(ResolveMessage(problemDetails, statusCode))
    {
        StatusCode = statusCode;
        ProblemDetails = problemDetails;
        ErrorCode = ExtractErrorCode(problemDetails?.Type);
        TraceId = problemDetails?.TraceId;
        UserMessage = ErrorCodeMessages.GetMessage(ErrorCode, problemDetails?.Status ?? statusCode);
    }

    public override string ToString()
    {
        return $"{base.ToString()} | StatusCode={StatusCode} | ErrorCode={ErrorCode} | TraceId={TraceId}";
    }

    private static string ResolveMessage(ProblemDetailsModel? problemDetails, int statusCode)
    {
        var code = ExtractErrorCode(problemDetails?.Type);
        return ErrorCodeMessages.GetMessage(code, problemDetails?.Status ?? statusCode);
    }

    private static string? ExtractErrorCode(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        var normalized = type.Trim();

        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            var segment = uri.Segments.LastOrDefault()?.Trim('/');
            return string.IsNullOrWhiteSpace(segment) ? normalized : segment;
        }

        var hashSplit = normalized.Split('#', StringSplitOptions.RemoveEmptyEntries);
        normalized = hashSplit.Length > 0 ? hashSplit[^1] : normalized;

        var slashSplit = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        return slashSplit.Length > 0 ? slashSplit[^1] : normalized;
    }
}
