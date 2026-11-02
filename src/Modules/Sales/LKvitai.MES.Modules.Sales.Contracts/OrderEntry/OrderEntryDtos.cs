using System.Text.Json.Serialization;
namespace LKvitai.MES.Modules.Sales.Contracts.OrderEntry;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrderHeaderInput(int SeriesId, string Number, DateOnly Date, DateOnly Due,
    int CustomerId, int StoreId, int? ManagerId, int? ConsultantId,
    int PriorityId = 2, DateOnly? MountingDate = null, string? Note = null, decimal DiscountPercent = 0);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateOrderCommand(Guid CommandId, OrderHeaderInput Header);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateOrderCommand(Guid CommandId, string ExpectedRevision, OrderHeaderInput Header);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateProductCommand(Guid CommandId, string ExpectedRevision, int TypeId);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ProductConfigurationInput(int TypeId, int WidthMm, int HeightMm, decimal Quantity,
    int ColourId, string? Control, decimal UnitPrice, bool UseListPrice,
    string? Note, IReadOnlyList<string?> Parameters, bool RecalculateParameters = true, IReadOnlyList<int>? ManualParameterSlots = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CalculationRevision = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ConfigureProductCommand(Guid CommandId, string ExpectedRevision, ProductConfigurationInput Product);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveAccessoryCommand(Guid CommandId, string ExpectedRevision, int AccessoryId, decimal Quantity, decimal UnitPrice);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeleteAccessoryCommand(Guid CommandId, string ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeleteProductCommand(Guid CommandId, string ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrderAddressInput(int StreetId, string? Building, string? Room, string? Phone, string? Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveOrderAddressCommand(Guid CommandId, string ExpectedRevision, OrderAddressInput Address);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record OrderDutyInput(int DutyId, int EmployeeId, DateOnly Date, string? Note);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SaveOrderDutyCommand(Guid CommandId, string ExpectedRevision, OrderDutyInput Duty);

public sealed record OrderHeaderSnapshot(long Id, string Number, int SeriesId, int BranchId,
    DateOnly? Date, DateOnly? Due, int? CustomerId, string? CustomerName,
    int? StoreId, string? StoreName, int? ManagerId, string? ManagerName,
    int? ConsultantId, string? ConsultantName, int? PriorityId, DateOnly? MountingDate,
    string? Note, decimal? DiscountPercent, int? StateId, string? StateName, bool Cancelled,
    bool CanEdit, decimal? StoredTotal, decimal CalculatedTotal, decimal Paid,
    decimal? DiscountableTotal = null, decimal? NonDiscountableTotal = null,
    IReadOnlyList<LKvitai.MES.Modules.Sales.Contracts.Orders.OrderNoteHistoryEntry>? NoteHistory = null);
public sealed record ProductSnapshot(long Id, int? TypeId, int? CategoryId, string? TypeName,
    string? CategoryName, int? WidthMm, int? HeightMm, decimal? Quantity, int ColourId,
    string? ColourCode, string? Control, decimal? UnitPrice, string? Note,
    IReadOnlyList<string?> Parameters, int? StateId, string? StateName, bool CanEdit,
    bool CanDelete, bool? TrustedPrice, decimal? Amount, IReadOnlyList<AccessorySnapshot> Accessories, decimal? AccessoryAmount = null,
    bool? IsManufactured = null,int? PriceUnitId = null);
public sealed record AccessorySnapshot(int AccessoryId, long ProductId, string? Name,
    string? Unit, decimal? Quantity, decimal? UnitPrice);
public sealed record DutySnapshot(int DutyId, int EmployeeId, string? DutyName,
    string? EmployeeName, DateTime Date, string? Note);
public sealed record OrderEditorSnapshot(OrderHeaderSnapshot Header, IReadOnlyList<ProductSnapshot> Products,
    OrderAddressInput? Address, IReadOnlyList<DutySnapshot> Duties, string Revision,
    bool WritesAllowed, IReadOnlyList<string> Warnings, bool PaymentsEnabled = false, bool ConfirmationEnabled = false,
    IReadOnlyList<int>? AllowedPaymentMethods = null, IReadOnlyList<OrderPaymentSnapshot>? Payments = null,
    string? CancellationUnavailableReason = null);
public sealed record OrderEntryCommandResult(OrderEditorSnapshot Order, long? ProductId, bool Replayed, long? PaymentId = null, int? ProductionDocumentId = null);
public sealed record CommandOutcome(Guid CommandId, string Operation, long OrderId, long? ProductId, long? PaymentId = null, int? ProductionDocumentId = null, IReadOnlyList<string>? Warnings = null);
public sealed class OrderEntryException(string code, string message, int status = 422) : Exception(message)
{ public string Code { get; } = code; public int Status { get; } = status; }

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CancelOrderCommand(Guid CommandId,string ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DeleteOrderDutyCommand(Guid CommandId,string ExpectedRevision);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SetOrderFinalPriceCommand(Guid CommandId,string ExpectedRevision,decimal FinalPrice);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DuplicateProductCommand(Guid CommandId,string ExpectedRevision);
