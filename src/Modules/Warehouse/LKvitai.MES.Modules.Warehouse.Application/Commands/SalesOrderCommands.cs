using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.SharedKernel;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

public sealed record SalesOrderLineCommand
{
    public int ItemId { get; init; }
    public decimal Qty { get; init; }
    public decimal UnitPrice { get; init; }
}

public sealed record CreateSalesOrderCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid SalesOrderId { get; init; } = Guid.NewGuid();
    public Guid CustomerId { get; init; }
    public Address? ShippingAddress { get; init; }
    public DateTime? RequestedDeliveryDate { get; init; }
    public List<SalesOrderLineCommand> Lines { get; init; } = new();
}

public sealed record SubmitSalesOrderCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid SalesOrderId { get; init; }
}

public sealed record ApproveSalesOrderCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid SalesOrderId { get; init; }
}

public sealed record AllocateSalesOrderCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid SalesOrderId { get; init; }
}

public sealed record ReleaseSalesOrderCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid SalesOrderId { get; init; }
}

public sealed record CancelSalesOrderCommand : ICommand
{
    public Guid CommandId { get; init; } = Guid.NewGuid();
    public Guid CorrelationId { get; init; }
    public Guid CausationId { get; init; }

    public Guid SalesOrderId { get; init; }
    public string Reason { get; init; } = string.Empty;
}
