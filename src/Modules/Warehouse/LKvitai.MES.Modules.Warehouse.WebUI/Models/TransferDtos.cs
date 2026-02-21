namespace LKvitai.MES.Modules.Warehouse.WebUI.Models;

public record TransferLineDto
{
    public int ItemId { get; init; }
    public decimal Qty { get; init; }
    public int FromLocationId { get; init; }
    public int ToLocationId { get; init; }
    public Guid? LotId { get; init; }
}

public record TransferDto
{
    public Guid Id { get; init; }
    public string TransferNumber { get; init; } = string.Empty;
    public string FromWarehouse { get; init; } = string.Empty;
    public string ToWarehouse { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string RequestedBy { get; init; } = string.Empty;
    public string? ApprovedBy { get; init; }
    public string? ExecutedBy { get; init; }
    public DateTimeOffset RequestedAt { get; init; }
    public DateTimeOffset? ApprovedAt { get; init; }
    public DateTimeOffset? ExecutedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public IReadOnlyList<TransferLineDto> Lines { get; init; } = Array.Empty<TransferLineDto>();
}

public record CreateTransferRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public string FromWarehouse { get; init; } = string.Empty;
    public string ToWarehouse { get; init; } = string.Empty;
    public IReadOnlyList<TransferLineDto> Lines { get; init; } = Array.Empty<TransferLineDto>();
    public string? RequestedBy { get; init; }
}

public record ApproveTransferRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public string? ApprovedBy { get; init; }
    public string? Reason { get; init; }
}

public record SubmitTransferRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
}

public record ExecuteTransferRequestDto
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
}
