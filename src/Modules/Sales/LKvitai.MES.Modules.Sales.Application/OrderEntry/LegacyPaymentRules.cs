using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.OrderEntry;

/// <summary>Executable current NaujasMokejimas rules; no receipt-catalog inference.</summary>
public static class LegacyPaymentRules
{
    public const int PpkMethodId=1;
    public const int BankMethodId=2;
    public const int CashMethodId=81423987;
    public const int RegisterMethodId=834019474;
    public const int BankDefaultEmployeeId=2138337249;
    public const int PpkReceiptLength=10;
    // Live Mokejimai.Suma is decimal(18,2). Trigger variables/Cash are MONEY;
    // Uzsakymai.Paid is SMALLMONEY. Bank source remains MONEY with four places.
    public const int AmountDecimalPlaces=2;
    public const decimal SmallestAmount=0.01m;
    public const decimal MaximumAmount=922337203685477.58m;
    public const decimal CashMaximum=922337203685477.5807m;
    public const decimal PaidMaximum=214748.3647m;
    public static bool IsValidPositiveAmount(decimal amount)=>OrderPaymentFormats.IsValidPositiveAmount(amount);
    public static decimal MaximumAdditionalAmount(decimal paid)=>Math.Max(0,
        Math.Min(MaximumAmount,decimal.Floor((PaidMaximum-paid)*100m)/100m));
    public static void ValidateBalanceAddition(decimal amount,decimal paid,decimal cash)
    {
        if(!IsValidPositiveAmount(amount))throw new OrderEntryException("PAYMENT_AMOUNT","Enter a positive amount with two decimal places, matching legacy payment storage.");
        if(paid+amount>PaidMaximum)throw new OrderEntryException("PAYMENT_PAID_RANGE","The resulting Paid amount would exceed legacy SMALLMONEY storage.");
        if(cash+amount<0||cash+amount>CashMaximum)throw new OrderEntryException("PAYMENT_CASH_RANGE","The resulting receiver cash amount is outside legacy MONEY storage.");
    }
    public static bool IsSupportedMethod(int id)=>id is PpkMethodId or BankMethodId or CashMethodId or RegisterMethodId;
    public static bool IsValidPpkReceipt(string? receipt)=>OrderPaymentFormats.IsValidPpkReceipt(receipt);
    public static bool IsReceiverAllowed(bool visibleInAllowedDepartment,bool explicitlyApproved,bool selectedRegisterDefault)
        =>visibleInAllowedDepartment||explicitlyApproved||selectedRegisterDefault;
}
