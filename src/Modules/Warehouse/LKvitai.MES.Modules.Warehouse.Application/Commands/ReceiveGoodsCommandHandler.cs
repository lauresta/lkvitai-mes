using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

/// <summary>
/// Handles <see cref="ReceiveGoodsCommand"/>.
/// Minimal Phase 1 implementation of ReceiveGoodsSaga (Req 15.1-15.9).
///
/// Delegates to <see cref="IReceiveGoodsOrchestration"/> which coordinates
/// multi-stream event appending in a single Marten transaction.
/// </summary>
public class ReceiveGoodsCommandHandler : IRequestHandler<ReceiveGoodsCommand, Result>
{
    private readonly IReceiveGoodsOrchestration _orchestration;
    private readonly ILogger<ReceiveGoodsCommandHandler> _logger;

    public ReceiveGoodsCommandHandler(
        IReceiveGoodsOrchestration orchestration,
        ILogger<ReceiveGoodsCommandHandler> logger)
    {
        _orchestration = orchestration;
        _logger = logger;
    }

    public async Task<Result> Handle(ReceiveGoodsCommand request, CancellationToken cancellationToken)
    {
        // Validate command
        if (string.IsNullOrWhiteSpace(request.WarehouseId))
            return Result.Fail("WarehouseId is required.");

        if (string.IsNullOrWhiteSpace(request.Location))
            return Result.Fail("Location is required.");

        if (request.Lines.Count == 0)
            return Result.Fail("At least one line is required for goods receipt.");

        foreach (var line in request.Lines)
        {
            if (string.IsNullOrWhiteSpace(line.SKU))
                return Result.Fail("SKU is required for each line.");
            if (line.Quantity <= 0)
                return Result.Fail($"Quantity must be positive for SKU '{line.SKU}'.");
        }

        var result = await _orchestration.ExecuteAsync(request, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation(
                "ReceiveGoods completed: HU {HuId} created at {Location} in warehouse {WarehouseId} with {LineCount} line(s)",
                result.Value, request.Location, request.WarehouseId, request.Lines.Count);
        }
        else
        {
            _logger.LogWarning("ReceiveGoods failed: {Error}", result.Error);
        }

        return result.IsSuccess ? Result.Ok() : Result.Fail(result.Error);
    }
}
