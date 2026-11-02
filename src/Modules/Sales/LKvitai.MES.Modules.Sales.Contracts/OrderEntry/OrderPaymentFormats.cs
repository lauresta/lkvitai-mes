using System.Text.RegularExpressions;
namespace LKvitai.MES.Modules.Sales.Contracts.OrderEntry;

/// <summary>Shared transport/input format, also validated by server payment rules.</summary>
public static class OrderPaymentFormats
{
    public const int PpkReceiptLength=10;
    public const int AmountDecimalPlaces=2;
    public const decimal MaximumAmount=922337203685477.58m;
    public static bool IsValidPositiveAmount(decimal amount)=>amount>0&&amount<=MaximumAmount
        &&decimal.Round(amount,AmountDecimalPlaces)==amount;
    public static bool IsValidPpkReceipt(string? receipt)=>receipt?.Length==PpkReceiptLength
        &&Regex.IsMatch(receipt,@"\A[RV][RVS][LS][0-9]{7}\z",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
}
