namespace LKvitai.MES.BuildingBlocks.SharedKernel;

/// <summary>
/// Async-flow correlation context populated by API middleware.
/// </summary>
public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> CorrelationIdValue = new();

    public static string? Current => CorrelationIdValue.Value;

    public static void Set(string correlationId) => CorrelationIdValue.Value = correlationId;

    public static void Clear() => CorrelationIdValue.Value = null;
}
