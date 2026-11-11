using LKvitai.MES.BuildingBlocks.SharedKernel;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class DistributeAgnumBalanceCommandHandler
    : IRequestHandler<DistributeAgnumBalanceCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IMediator _mediator;
    private readonly ILogger<DistributeAgnumBalanceCommandHandler> _logger;
    private readonly IWarehouseCodeProvider _warehouseCode;

    public DistributeAgnumBalanceCommandHandler(
        WarehouseDbContext dbContext,
        IMediator mediator,
        ILogger<DistributeAgnumBalanceCommandHandler> logger,
        IWarehouseCodeProvider warehouseCode)
    {
        _warehouseCode = warehouseCode;
        _dbContext = dbContext;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<Result> Handle(DistributeAgnumBalanceCommand command, CancellationToken ct)
    {
        var virtualBalance = await _dbContext.AgnumVirtualWarehouseBalances
            .FirstOrDefaultAsync(x => x.Id == command.VirtualBalanceId, ct);

        if (virtualBalance is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Agnum virtual balance '{command.VirtualBalanceId}' was not found.");
        }

        if (string.IsNullOrWhiteSpace(virtualBalance.Sku))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Agnum product must be linked to a MES item before distribution.");
        }

        if (string.IsNullOrWhiteSpace(command.LocationCode))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Location code is required.");
        }

        // The warehouse is not a choice: stock is always distributed to the single configured warehouse.
        var warehouseCode = _warehouseCode.Code;
        if (!string.IsNullOrWhiteSpace(command.WarehouseId) &&
            !string.Equals(command.WarehouseId.Trim(), warehouseCode, StringComparison.OrdinalIgnoreCase))
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Agnum balances are distributed only to warehouse '{warehouseCode}'.");
        }

        var alreadyDistributed = await _dbContext.AgnumBalanceDistributions
            .Where(x => x.VirtualBalanceId == command.VirtualBalanceId)
            .SumAsync(x => (decimal?)x.Quantity, ct) ?? 0m;

        var remaining = virtualBalance.Quantity - alreadyDistributed;
        if (command.Quantity <= 0m || command.Quantity > remaining)
        {
            return Result.Fail(
                DomainErrorCodes.ValidationError,
                $"Quantity must be greater than 0 and no more than remaining quantity {remaining:0.####}.");
        }

        var huCommandId = Guid.NewGuid();
        var receiveResult = await _mediator.Send(new ReceiveGoodsCommand
        {
            CommandId = huCommandId,
            CorrelationId = command.CommandId,
            CausationId = command.CommandId,
            WarehouseId = warehouseCode,
            Location = command.LocationCode.Trim(),
            FromLocation = "AGNUM",
            HuType = "PALLET",
            OperatorId = command.OperatorId,
            Lines =
            [
                new ReceiveGoodsLineDto
                {
                    SKU = virtualBalance.Sku.Trim(),
                    Quantity = command.Quantity
                }
            ]
        }, ct);

        if (!receiveResult.IsSuccess)
        {
            return receiveResult;
        }

        _dbContext.AgnumBalanceDistributions.Add(new AgnumBalanceDistribution
        {
            Id = Guid.NewGuid(),
            VirtualBalanceId = virtualBalance.Id,
            SndId = virtualBalance.SndId,
            AgnumProductId = virtualBalance.AgnumProductId,
            Sku = virtualBalance.Sku.Trim(),
            LocationCode = command.LocationCode.Trim(),
            WarehouseId = warehouseCode,
            Quantity = command.Quantity,
            StockMovementCommandId = huCommandId,
            DistributedAt = DateTime.UtcNow,
            DistributedBy = command.OperatorId.ToString()
        });

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Agnum balance {VirtualBalanceId} distributed: {Quantity} {Sku} to {WarehouseId}/{LocationCode}",
            virtualBalance.Id,
            command.Quantity,
            virtualBalance.Sku,
            warehouseCode,
            command.LocationCode);

        return Result.Ok();
    }
}
