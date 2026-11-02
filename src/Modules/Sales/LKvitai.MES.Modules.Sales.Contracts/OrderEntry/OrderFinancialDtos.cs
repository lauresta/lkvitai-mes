using System.Text.Json.Serialization;
namespace LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrderPaymentInput(DateOnly Date,decimal Amount,int MethodId,int EmployeeId,
    int? BankTransferId=null,string? PpkReceipt=null,int? RegisterId=null,string? Note=null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PostOrderPaymentCommand(Guid CommandId,string ExpectedRevision,OrderPaymentInput Payment);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfirmOrderProductsCommand(Guid CommandId,string ExpectedRevision,IReadOnlyList<long> ProductIds);
public sealed record OrderPaymentSnapshot(long Id,DateOnly? Date,decimal Amount,int? MethodId,int? EmployeeId,
    int? BankTransferId,string? PpkReceipt,int? RegisterId,string? Note,int? CurrencyId,string? CurrencyCode=null,
    string? MethodName=null,string? EmployeeName=null,string? RegisterName=null);
public sealed record FinancialCommandResult(OrderEntryCommandResult Result,long? PaymentId,int? ProductionDocumentId);

public sealed record OrderPaymentMethodReference(int Id,string Name,bool RequiresPpkReceipt=false,
    bool RequiresBankTransfer=false,bool RequiresRegister=false,int? DefaultEmployeeId=null,
    bool IsSupported=true,string? UnavailableReason=null);
public sealed record OrderPaymentEmployeeReference(int Id,string Name,int? BranchId=null,bool IsHidden=false);
public sealed record OrderPaymentCurrencyReference(int Id,string Code,decimal Rate);
public sealed record OrderPaymentRegisterReference(int Id,string Name,int? EmployeeId,int? BranchId=null,
    string? DefaultEmployeeName=null,bool DefaultEmployeeIsHidden=false)
{ public int? DefaultEmployeeId=>EmployeeId; }
public sealed record OrderPaymentBankTransferReference(int Id,DateOnly? Date,decimal AvailableAmount,
    string? Payer=null,string? Purpose=null,decimal? OriginalAmount=null,decimal? AssignedAmount=null,
    bool IsAvailable=true,string? UnavailableReason=null,int? DefaultEmployeeId=null,
    string AmountRule="legacy-raw-no-conversion",string? SourceCurrencyCode=null,string? PostingCurrencyCode=null);
public sealed record OrderPaymentReferenceSelection(int? MethodId=null,int? BankTransferId=null,int? RegisterId=null);
public sealed record OrderPaymentBankTransferPage(IReadOnlyList<OrderPaymentBankTransferReference> Items,
    int TotalCount,int Page,int PageSize,OrderPaymentBankTransferReference? Selected=null);
public sealed record OrderPaymentReferences(bool Enabled,string? UnavailableReason,DateOnly BusinessDate,
    string OrderRevision,OrderPaymentCurrencyReference? Currency,int? DefaultEmployeeId,int? DefaultMethodId,
    IReadOnlyList<OrderPaymentMethodReference> Methods,IReadOnlyList<OrderPaymentEmployeeReference> Employees,
    IReadOnlyList<OrderPaymentRegisterReference> Registers,IReadOnlyList<OrderPaymentBankTransferReference> BankTransfers,
    OrderPaymentReferenceSelection? Selection=null,OrderPaymentBankTransferReference? SelectedBankTransfer=null,
    OrderPaymentRegisterReference? SelectedRegister=null,string? PrerequisiteError=null,
    int AmountDecimalPlaces=2,decimal? MaximumAmount=null);
