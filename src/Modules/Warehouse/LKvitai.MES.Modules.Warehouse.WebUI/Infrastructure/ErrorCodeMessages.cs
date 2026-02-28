namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

public static class ErrorCodeMessages
{
    private static readonly IReadOnlyDictionary<string, string> Messages =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["INSUFFICIENT_BALANCE"] = "Insufficient balance. Cannot complete operation.",
            ["INSUFFICIENT_AVAILABLE_STOCK"] = "Insufficient stock at source location. Check available quantities before transferring.",
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
            400 => "Request data is invalid. Check entered values.",
            401 => "Authentication required.",
            403 => "You do not have permission to perform this action.",
            404 => "The requested resource was not found.",
            409 => "Operation cannot be completed due to a conflict. Refresh data and review current state.",
            422 => "Operation cannot be completed due to business rules. Review details and adjust input.",
            429 => "Too many requests. Please wait and try again.",
            500 => "Server error. Please try again later.",
            503 => "Backend unavailable. Please check system status.",
            _ => "An unexpected error occurred. Please try again."
        };
    }
}
