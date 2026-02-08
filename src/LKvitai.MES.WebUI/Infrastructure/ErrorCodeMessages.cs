namespace LKvitai.MES.WebUI.Infrastructure;

public static class ErrorCodeMessages
{
    private static readonly IReadOnlyDictionary<string, string> Messages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["INSUFFICIENT_BALANCE"] = "Insufficient balance. Cannot complete operation.",
            ["RESERVATION_NOT_ALLOCATED"] = "Reservation is not in ALLOCATED state. Cannot start picking.",
            ["HARD_LOCK_CONFLICT"] = "HARD lock conflict detected. Another reservation is already picking this stock.",
            ["INVALID_PROJECTION_NAME"] = "Invalid projection name. Must be LocationBalance or AvailableStock.",
            ["IDEMPOTENCY_IN_PROGRESS"] = "Request is currently being processed. Please wait.",
            ["IDEMPOTENCY_ALREADY_PROCESSED"] = "Request already processed. Idempotency key conflict.",
            ["CONCURRENCY_CONFLICT"] = "Concurrent modification detected. Please retry.",
            ["VALIDATION_ERROR"] = "One or more validation errors occurred.",
            ["NOT_FOUND"] = "The requested resource was not found.",
            ["UNAUTHORIZED"] = "Authentication required.",
            ["FORBIDDEN"] = "You do not have permission to perform this action.",
            ["INTERNAL_ERROR"] = "Server error. Please try again later."
        };

    public static string GetMessage(string? errorCode, int? httpStatus)
    {
        if (!string.IsNullOrWhiteSpace(errorCode) && Messages.TryGetValue(errorCode, out var message))
        {
            return message;
        }

        return httpStatus switch
        {
            500 => "Server error. Please try again later.",
            503 => "Backend unavailable. Please check system status.",
            _ => "An unexpected error occurred. Please try again."
        };
    }
}
