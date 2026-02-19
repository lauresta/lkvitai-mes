using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Domain;

/// <summary>
/// Thrown when a stock movement would result in negative balance at a location.
/// This is a domain invariant violation and must NOT be retried.
/// </summary>
public class InsufficientBalanceException : DomainException
{
    public string Location { get; }
    public string SKU { get; }
    public decimal RequestedQuantity { get; }
    public decimal AvailableQuantity { get; }

    public InsufficientBalanceException(
        string location, string sku, decimal requested, decimal available)
        : base(
            DomainErrorCodes.InsufficientBalance,
            $"Insufficient balance at '{location}' for SKU '{sku}': " +
            $"requested {requested}, available {available}")
    {
        Location = location;
        SKU = sku;
        RequestedQuantity = requested;
        AvailableQuantity = available;
    }
}
