namespace LKvitai.MES.WebUI.Models;

public record SalesOrderAddressDto
{
    public string Street { get; init; } = string.Empty;
    public string City { get; init; } = string.Empty;
    public string State { get; init; } = string.Empty;
    public string ZipCode { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}

public record SalesOrderLineDto
{
    public Guid Id { get; init; }
    public int ItemId { get; init; }
    public string ItemSku { get; init; } = string.Empty;
    public string ItemDescription { get; init; } = string.Empty;
    public decimal OrderedQty { get; init; }
    public decimal AllocatedQty { get; init; }
    public decimal PickedQty { get; init; }
    public decimal ShippedQty { get; init; }
    public decimal UnitPrice { get; init; }
    public decimal LineAmount { get; init; }
}

public record SalesOrderDto
{
    public Guid Id { get; init; }
    public string OrderNumber { get; init; } = string.Empty;
    public Guid CustomerId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public SalesOrderAddressDto? ShippingAddress { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime OrderDate { get; init; }
    public DateTime? RequestedDeliveryDate { get; init; }
    public DateTime? AllocatedAt { get; init; }
    public DateTime? ShippedAt { get; init; }
    public IReadOnlyList<SalesOrderLineDto> Lines { get; init; } = Array.Empty<SalesOrderLineDto>();
    public Guid? ReservationId { get; init; }
    public Guid? OutboundOrderId { get; init; }
    public decimal TotalAmount { get; init; }
}

public record SalesOrderLineCreateRequestDto
{
    public int ItemId { get; init; }
    public decimal Qty { get; init; }
    public decimal UnitPrice { get; init; }
}

public record SalesOrderCreateRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public SalesOrderAddressDto? ShippingAddress { get; init; }
    public DateTime? RequestedDeliveryDate { get; init; }
    public IReadOnlyList<SalesOrderLineCreateRequestDto> Lines { get; init; } = Array.Empty<SalesOrderLineCreateRequestDto>();
}

public record SalesOrderCommandRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
}

public record CancelSalesOrderRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public string Reason { get; init; } = string.Empty;
}

public record SalesOrderAllocationResponseDto
{
    public Guid? ReservationId { get; init; }
}

public record SalesOrderCustomerLookupDto
{
    public Guid Id { get; init; }
    public string CustomerCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public SalesOrderAddressDto? DefaultShippingAddress { get; init; }
}
