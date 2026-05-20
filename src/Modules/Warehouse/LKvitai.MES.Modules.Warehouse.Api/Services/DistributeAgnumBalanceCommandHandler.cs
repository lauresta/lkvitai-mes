using LKvitai.MES.BuildingBlocks.SharedKernel;
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

    public DistributeAgnumBalanceCommandHandler(
        WarehouseDbContext dbContext,
        IMediator mediator,
        ILogger<DistributeAgnumBalanceCommandHandler> logger)
    {
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

        if (string.IsNullOrWhiteSpace(command.WarehouseId))
        {
            return Result.Fail(DomainErrorCodes.ValidationError, "Warehouse id is required.");
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

        var stockMovementCommandId = Guid.NewGuid();
        var stockMovementResult = await _mediator.Send(new RecordStockMovementCommand
        {
            CommandId = stockMovementCommandId,
            CorrelationId = command.CommandId,
            CausationId = command.CommandId,
            WarehouseId = command.WarehouseId.Trim(),
            SKU = virtualBalance.Sku.Trim(),
            Quantity = command.Quantity,
            FromLocation = "AGNUM",
            ToLocation = command.LocationCode.Trim(),
            MovementType = MovementType.Receipt,
            OperatorId = command.OperatorId,
            Reason = $"Agnum distribution sndid={virtualBalance.SndId}"
        }, ct);

        if (!stockMovementResult.IsSuccess)
        {
            return stockMovementResult;
        }

        _dbContext.AgnumBalanceDistributions.Add(new AgnumBalanceDistribution
        {
            Id = Guid.NewGuid(),
            VirtualBalanceId = virtualBalance.Id,
            SndId = virtualBalance.SndId,
            AgnumProductId = virtualBalance.AgnumProductId,
            Sku = virtualBalance.Sku.Trim(),
            LocationCode = command.LocationCode.Trim(),
            WarehouseId = command.WarehouseId.Trim(),
            Quantity = command.Quantity,
            StockMovementCommandId = stockMovementCommandId,
            DistributedAt = DateTime.UtcNow,
            DistributedBy = command.OperatorId.ToString()
        });

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Agnum balance {VirtualBalanceId} distributed: {Quantity} {Sku} to {WarehouseId}/{LocationCode}",
            virtualBalance.Id,
            command.Quantity,
            virtualBalance.Sku,
            command.WarehouseId,
            command.LocationCode);

        return Result.Ok();
    }
}
