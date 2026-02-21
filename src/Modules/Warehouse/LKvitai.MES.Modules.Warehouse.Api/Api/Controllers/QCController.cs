using System.Diagnostics;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Authorize(Policy = WarehousePolicies.QcOrManager)]
[Route("api/warehouse/v1/qc")]
public sealed class QCController : ControllerBase
{
    private const string DefaultWarehouseId = "WH1";

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly IElectronicSignatureService _signatureService;

    public QCController(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        IElectronicSignatureService signatureService)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _signatureService = signatureService;
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var querySession = _documentStore.QuerySession();

        var rows = await Marten.QueryableExtensions.ToListAsync(
            querySession.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == DefaultWarehouseId &&
                            x.Location == "QC_HOLD" &&
                            x.AvailableQty > 0m),
            cancellationToken);

        var skus = rows.Select(x => x.SKU).Distinct().ToList();
        var items = await _dbContext.Items
            .AsNoTracking()
            .Where(x => skus.Contains(x.InternalSKU))
            .ToDictionaryAsync(x => x.InternalSKU, cancellationToken);

        var lotNumbers = rows
            .Select(x => x.LotNumber)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct()
            .ToList();

        var lots = await _dbContext.Lots
            .AsNoTracking()
            .Where(x => lotNumbers.Contains(x.LotNumber))
            .ToListAsync(cancellationToken);

        var pending = rows
            .OrderBy(x => x.SKU)
            .ThenBy(x => x.LotNumber)
            .Select(row =>
            {
                items.TryGetValue(row.SKU, out var item);
                var resolvedItemId = item?.Id ?? row.ItemId ?? 0;
                if (resolvedItemId <= 0)
                {
                    return null;
                }

                var lot = row.LotNumber is null
                    ? null
                    : lots.FirstOrDefault(x => x.ItemId == resolvedItemId && x.LotNumber == row.LotNumber);

                return new QcPendingRowResponse(
                    resolvedItemId,
                    row.SKU,
                    item?.Name ?? row.ItemName ?? row.SKU,
                    lot?.Id,
                    row.LotNumber,
                    row.AvailableQty,
                    row.BaseUoM ?? item?.BaseUoM ?? string.Empty);
            })
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        return Ok(pending);
    }

    [HttpPost("pass")]
    public Task<IActionResult> PassAsync([FromBody] QcActionRequest request, CancellationToken cancellationToken = default)
        => ProcessQcAsync(request, true, cancellationToken);

    [HttpPost("fail")]
    public Task<IActionResult> FailAsync([FromBody] QcActionRequest request, CancellationToken cancellationToken = default)
        => ProcessQcAsync(request, false, cancellationToken);

    private async Task<IActionResult> ProcessQcAsync(
        QcActionRequest request,
        bool isPass,
        CancellationToken cancellationToken)
    {
        if (request.ItemId <= 0)
        {
            return ValidationFailure("Field 'itemId' is required.");
        }

        if (request.Qty <= 0m)
        {
            return ValidationFailure("Field 'qty' must be greater than 0.");
        }

        if (!isPass && string.IsNullOrWhiteSpace(request.ReasonCode))
        {
            return ValidationFailure("Field 'reasonCode' is required for QC fail.");
        }

        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.ItemId, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item '{request.ItemId}' does not exist."));
        }

        if (request.LotId.HasValue)
        {
            var lotExists = await _dbContext.Lots
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.LotId.Value && x.ItemId == item.Id, cancellationToken);
            if (!lotExists)
            {
                return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Lot '{request.LotId.Value}' does not exist for item '{item.Id}'."));
            }
        }

        if (!isPass)
        {
            var normalizedReasonCode = request.ReasonCode!.Trim().ToUpperInvariant();
            var reasonExists = await _dbContext.AdjustmentReasonCodes
                .AsNoTracking()
                .AnyAsync(x => x.Code == normalizedReasonCode && x.Active, cancellationToken);
            if (!reasonExists)
            {
                return ValidationFailure($"ReasonCode '{request.ReasonCode}' does not exist.");
            }
        }

        await using var querySession = _documentStore.QuerySession();
        var availableQcQty = await Marten.QueryableExtensions.SumAsync(
            querySession.Query<AvailableStockView>()
                .Where(x => x.WarehouseId == DefaultWarehouseId && x.Location == "QC_HOLD" && x.SKU == item.InternalSKU),
            x => x.AvailableQty,
            cancellationToken);

        if (availableQcQty < request.Qty)
        {
            return UnprocessableFailure(
                $"Insufficient stock in QC_HOLD for item '{item.InternalSKU}'. Available {availableQcQty}, requested {request.Qty}.");
        }

        var now = DateTime.UtcNow;
        WarehouseOperationalEvent evt = isPass
            ? new QCPassedEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = _currentUserService.GetCurrentUserId(),
                TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                WarehouseId = DefaultWarehouseId,
                ItemId = item.Id,
                SKU = item.InternalSKU,
                Qty = request.Qty,
                FromLocation = "QC_HOLD",
                ToLocation = "RECEIVING",
                LotId = request.LotId,
                InspectorNotes = request.InspectorNotes,
                Timestamp = now
            }
            : new QCFailedEvent
            {
                AggregateId = Guid.NewGuid(),
                UserId = _currentUserService.GetCurrentUserId(),
                TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                WarehouseId = DefaultWarehouseId,
                ItemId = item.Id,
                SKU = item.InternalSKU,
                Qty = request.Qty,
                FromLocation = "QC_HOLD",
                ToLocation = "QUARANTINE",
                LotId = request.LotId,
                ReasonCode = request.ReasonCode!.Trim().ToUpperInvariant(),
                InspectorNotes = request.InspectorNotes,
                Timestamp = now
            };

        await using (var session = _documentStore.LightweightSession())
        {
            session.Events.Append($"qc-task:{Guid.NewGuid():N}", evt);
            await session.SaveChangesAsync(cancellationToken);
        }

        await TryCaptureSignatureAsync(request, item.InternalSKU, isPass, cancellationToken);

        return Ok(new QcActionResponse(
            evt.EventId,
            item.Id,
            request.Qty,
            isPass ? "RECEIVING" : "QUARANTINE",
            now));
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private ObjectResult UnprocessableFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity
        };
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    public sealed record QcActionRequest(
        int ItemId,
        int? LotId,
        decimal Qty,
        string? ReasonCode,
        string? InspectorNotes,
        string? SignatureText = null,
        string? SignaturePassword = null,
        string? SignatureMeaning = null);

    public sealed record QcActionResponse(
        Guid EventId,
        int ItemId,
        decimal Qty,
        string DestinationLocationCode,
        DateTime Timestamp);

    public sealed record QcPendingRowResponse(
        int ItemId,
        string ItemSku,
        string ItemName,
        int? LotId,
        string? LotNumber,
        decimal Qty,
        string BaseUoM);

    private async Task TryCaptureSignatureAsync(
        QcActionRequest request,
        string itemSku,
        bool isPass,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.SignatureText) ||
            string.IsNullOrWhiteSpace(request.SignaturePassword))
        {
            return;
        }

        try
        {
            await _signatureService.CaptureAsync(new CaptureSignatureCommand(
                isPass ? "QC_APPROVAL" : "QC_REJECT",
                request.LotId?.ToString() ?? itemSku,
                request.SignatureText!,
                request.SignatureMeaning ?? (isPass ? "APPROVED" : "REJECTED"),
                _currentUserService.GetCurrentUserId() ?? "unknown",
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                request.SignaturePassword), cancellationToken);
        }
        catch (Exception ex)
        {
            HttpContext?.RequestServices.GetRequiredService<ILogger<QCController>>()
                .LogWarning(ex, "Electronic signature capture failed for QC action");
        }
    }
}
