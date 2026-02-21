namespace LKvitai.MES.BuildingBlocks.SharedKernel;

/// <summary>
/// Exception thrown when an optimistic concurrency conflict is detected
/// during event stream append operations.
/// </summary>
public class ConcurrencyException : Exception
{
    public ConcurrencyException(string message) : base(message)
    {
    }

    public ConcurrencyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
