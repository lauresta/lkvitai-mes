using System.Diagnostics;
using System.Globalization;
using System.Text;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using IDocumentStore = Marten.IDocumentStore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/receiving/shipments")]
[Route("api/warehouse/v1/inbound-shipments")]
public sealed class ReceivingController : ControllerBase
{
    private const string DefaultWarehouseId = "WH1";

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;

    public ReceivingController(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetShipmentsAsync(
        [FromQuery] int? supplierId,
        [FromQuery] string? status,
        [FromQuery] DateOnly? expectedDateFrom,
        [FromQuery] DateOnly? expectedDateTo,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] bool exportCsv = false,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        await using var querySession = _documentStore.QuerySession();

        IQueryable<InboundShipmentSummaryView> query = querySession.Query<InboundShipmentSummaryView>();

        if (supplierId.HasValue)
        {
            query = query.Where(x => x.SupplierId == supplierId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            query = query.Where(x => x.Status == normalized);
        }

        if (expectedDateFrom.HasValue)
        {
            var from = expectedDateFrom.Value;
            query = query.Where(x => x.ExpectedDate != null && x.ExpectedDate >= from);
        }

        if (expectedDateTo.HasValue)
        {
            var to = expectedDateTo.Value;
            query = query.Where(x => x.ExpectedDate != null && x.ExpectedDate <= to);
        }

        if (exportCsv)
        {
            var csvRows = await Marten.QueryableExtensions.ToListAsync(
                query.OrderByDescending(x => x.LastUpdated),
                cancellationToken);

            return File(
                Encoding.UTF8.GetBytes(BuildShipmentsCsv(csvRows)),
                "text/csv",
                "receiving-history.csv");
        }

        var totalCount = await Marten.QueryableExtensions.CountAsync(query, cancellationToken);
        var items = await Marten.QueryableExtensions.ToListAsync(
            query.OrderByDescending(x => x.LastUpdated)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize),
            cancellationToken);

        return Ok(new PagedResponse<InboundShipmentSummaryDto>(
            items.Select(x => new InboundShipmentSummaryDto(
                x.ShipmentId,
                x.ReferenceNumber,
                x.SupplierId,
                x.SupplierName,
                x.ExpectedDate,
                x.Status,
                x.TotalLines,
                x.TotalExpectedQty,
                x.TotalReceivedQty,
                x.CreatedAt,
                x.LastUpdated)).ToList(),
            totalCount,
            pageNumber,
            pageSize));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetShipmentByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var shipment = await _dbContext.InboundShipments
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shipment is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Shipment '{id}' does not exist."));
        }

        var itemIds = shipment.Lines.Select(x => x.ItemId).Distinct().ToList();
        var primaryPhotos = await _dbContext.ItemPhotos
            .AsNoTracking()
            .Where(x => itemIds.Contains(x.ItemId) && x.IsPrimary)
            .Select(x => new { x.ItemId, x.Id })
            .ToDictionaryAsync(
                x => x.ItemId,
                x => ItemPhotoService.BuildProxyUrl(x.ItemId, x.Id, "thumb"),
                cancellationToken);

        return Ok(new InboundShipmentDetailDto(
            shipment.Id,
            shipment.ReferenceNumber,
            shipment.SupplierId,
            shipment.Supplier?.Name ?? string.Empty,
            shipment.ExpectedDate,
            shipment.Status,
            shipment.CreatedAt,
            shipment.UpdatedAt,
            shipment.Lines
                .OrderBy(x => x.Id)
                .Select(x => new InboundShipmentLineDetailDto(
                    x.Id,
                    x.ItemId,
                    x.Item?.InternalSKU ?? string.Empty,
                    x.Item?.Name ?? string.Empty,
                    x.Item?.PrimaryBarcode,
                    primaryPhotos.TryGetValue(x.ItemId, out var primaryThumbUrl) ? primaryThumbUrl : null,
                    x.Item?.RequiresLotTracking ?? false,
                    x.Item?.RequiresQC ?? false,
                    x.ExpectedQty,
                    x.ReceivedQty,
                    x.BaseUoM))
                .ToList()));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> CreateShipmentAsync(
        [FromBody] CreateInboundShipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ReferenceNumber))
        {
            return ValidationFailure("Field 'referenceNumber' is required.");
        }

        if (request.SupplierId <= 0)
        {
            return ValidationFailure("Field 'supplierId' is required.");
        }

        if (request.Lines.Count == 0)
        {
            return ValidationFailure("At least one shipment line is required.");
        }

        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return ValidationFailure($"Supplier '{request.SupplierId}' does not exist.");
        }

        var lineItemIds = request.Lines.Select(x => x.ItemId).Distinct().ToList();
        var items = await _dbContext.Items
            .AsNoTracking()
            .Where(x => lineItemIds.Contains(x.Id))
            .Select(x => new { x.Id, x.BaseUoM })
            .ToListAsync(cancellationToken);

        if (items.Count != lineItemIds.Count)
        {
            return ValidationFailure("One or more line ItemId values do not exist.");
        }

        if (request.Lines.Any(x => x.ExpectedQty <= 0m))
        {
            return ValidationFailure("All shipment line expectedQty values must be greater than 0.");
        }

        var shipment = new InboundShipment
        {
            ReferenceNumber = request.ReferenceNumber.Trim(),
            SupplierId = request.SupplierId,
            ExpectedDate = request.ExpectedDate,
            Status = "Draft"
        };

        foreach (var line in request.Lines)
        {
            var item = items.First(x => x.Id == line.ItemId);
            shipment.Lines.Add(new InboundShipmentLine
            {
                ItemId = line.ItemId,
                ExpectedQty = line.ExpectedQty,
                ReceivedQty = 0m,
                BaseUoM = item.BaseUoM
            });
        }

        _dbContext.InboundShipments.Add(shipment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var evt = new InboundShipmentCreatedEvent
        {
            AggregateId = Guid.NewGuid(),
            UserId = _currentUserService.GetCurrentUserId(),
            TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            ShipmentId = shipment.Id,
            ReferenceNumber = shipment.ReferenceNumber,
            SupplierId = shipment.SupplierId,
            SupplierName = supplier.Name,
            ExpectedDate = shipment.ExpectedDate,
            TotalLines = shipment.Lines.Count,
            TotalExpectedQty = shipment.Lines.Sum(x => x.ExpectedQty),
            Timestamp = now
        };

        await using (var session = _documentStore.LightweightSession())
        {
            session.Events.Append(ShipmentStreamId(shipment.Id), evt);
            await session.SaveChangesAsync(cancellationToken);
        }

        return Created(
            $"/api/warehouse/v1/receiving/shipments/{shipment.Id}",
            new ShipmentCreatedResponse(
                shipment.Id,
                shipment.ReferenceNumber,
                shipment.Status,
                now));
    }

    [HttpPost("{id:int}/receive")]
    [HttpPost("{id:int}/receive-items")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> ReceiveGoodsAsync(
        int id,
        [FromBody] ReceiveShipmentLineRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.LineId <= 0)
        {
            return ValidationFailure("Field 'lineId' is required.");
        }

        if (request.ReceivedQty <= 0m)
        {
            return ValidationFailure("Field 'receivedQty' must be greater than 0.");
        }

        var shipment = await _dbContext.InboundShipments
            .Include(x => x.Lines)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shipment is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Shipment '{id}' does not exist."));
        }

        var line = shipment.Lines.FirstOrDefault(x => x.Id == request.LineId);
        if (line is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Shipment line '{request.LineId}' does not exist."));
        }

        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == line.ItemId, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item '{line.ItemId}' does not exist."));
        }

        if (line.ReceivedQty + request.ReceivedQty > line.ExpectedQty)
        {
            return ValidationFailure(
                $"Received quantity would exceed expected quantity ({line.ExpectedQty}) for line '{line.Id}'.");
        }

        if (item.RequiresLotTracking && string.IsNullOrWhiteSpace(request.LotNumber))
        {
            return UnprocessableFailure(
                $"Item '{item.InternalSKU}' requires lot tracking. LotNumber must be provided.");
        }

        Lot? lot = null;
        if (!string.IsNullOrWhiteSpace(request.LotNumber))
        {
            var normalizedLot = request.LotNumber.Trim();
            lot = await _dbContext.Lots
                .FirstOrDefaultAsync(x => x.ItemId == item.Id && x.LotNumber == normalizedLot, cancellationToken);

            if (lot is null)
            {
                lot = new Lot
                {
                    ItemId = item.Id,
                    LotNumber = normalizedLot,
                    ProductionDate = request.ProductionDate,
                    ExpiryDate = request.ExpiryDate
                };

                _dbContext.Lots.Add(lot);
            }
            else
            {
                lot.ProductionDate ??= request.ProductionDate;
                lot.ExpiryDate ??= request.ExpiryDate;
            }
        }

        var destinationCode = item.RequiresQC ? "QC_HOLD" : "RECEIVING";
        var destinationLocation = await _dbContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == destinationCode, cancellationToken);

        if (destinationLocation is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.InternalError, $"Virtual location '{destinationCode}' is missing."));
        }

        line.ReceivedQty += request.ReceivedQty;

        var allComplete = shipment.Lines.All(x => x.ReceivedQty >= x.ExpectedQty);
        var anyReceived = shipment.Lines.Any(x => x.ReceivedQty > 0m);
        shipment.Status = allComplete
            ? "Complete"
            : anyReceived
                ? "Partial"
                : "Draft";

        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var evt = new GoodsReceivedEvent
        {
            AggregateId = Guid.NewGuid(),
            UserId = _currentUserService.GetCurrentUserId(),
            TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            WarehouseId = DefaultWarehouseId,
            ShipmentId = shipment.Id,
            LineId = line.Id,
            ItemId = item.Id,
            SKU = item.InternalSKU,
            ReceivedQty = request.ReceivedQty,
            BaseUoM = line.BaseUoM,
            DestinationLocationId = destinationLocation.Id,
            DestinationLocation = destinationCode,
            LotId = lot?.Id,
            LotNumber = lot?.LotNumber,
            ProductionDate = lot?.ProductionDate,
            ExpiryDate = lot?.ExpiryDate,
            SupplierId = shipment.SupplierId,
            Notes = request.Notes,
            Timestamp = now
        };

        await using (var session = _documentStore.LightweightSession())
        {
            session.Events.Append(ShipmentStreamId(shipment.Id), evt);
            await session.SaveChangesAsync(cancellationToken);
        }

        return Ok(new ReceiveGoodsResponse(
            shipment.Id,
            line.Id,
            item.Id,
            request.ReceivedQty,
            lot?.Id,
            destinationLocation.Id,
            destinationCode,
            evt.EventId,
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

    private static string ShipmentStreamId(int shipmentId)
        => $"inbound-shipment:{shipmentId}";

    private static string BuildShipmentsCsv(IReadOnlyCollection<InboundShipmentSummaryView> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ShipmentId,ReferenceNumber,SupplierId,SupplierName,ExpectedDate,Status,TotalLines,TotalExpectedQty,TotalReceivedQty,CreatedAt,LastUpdated");

        foreach (var row in rows)
        {
            sb.Append(row.ShipmentId.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(row.ReferenceNumber)).Append(',');
            sb.Append(row.SupplierId.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(EscapeCsv(row.SupplierName)).Append(',');
            sb.Append(row.ExpectedDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(EscapeCsv(row.Status)).Append(',');
            sb.Append(row.TotalLines.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.TotalExpectedQty.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.TotalReceivedQty.ToString(CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.CreatedAt.ToString("O", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.LastUpdated.ToString("O", CultureInfo.InvariantCulture));
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    public sealed record CreateInboundShipmentRequest(
        string ReferenceNumber,
        int SupplierId,
        string? Type,
        DateOnly? ExpectedDate,
        IReadOnlyList<CreateInboundShipmentLineRequest> Lines);

    public sealed record CreateInboundShipmentLineRequest(int ItemId, decimal ExpectedQty);

    public sealed record ShipmentCreatedResponse(int Id, string ReferenceNumber, string Status, DateTime CreatedAt);

    public sealed record ReceiveShipmentLineRequest(
        int LineId,
        decimal ReceivedQty,
        string? LotNumber,
        DateOnly? ProductionDate,
        DateOnly? ExpiryDate,
        string? Notes);

    public sealed record ReceiveGoodsResponse(
        int ShipmentId,
        int LineId,
        int ItemId,
        decimal ReceivedQty,
        int? LotId,
        int DestinationLocationId,
        string DestinationLocationCode,
        Guid EventId,
        DateTime Timestamp);

    public sealed record InboundShipmentSummaryDto(
        int Id,
        string ReferenceNumber,
        int SupplierId,
        string SupplierName,
        DateOnly? ExpectedDate,
        string Status,
        int TotalLines,
        decimal TotalExpectedQty,
        decimal TotalReceivedQty,
        DateTimeOffset CreatedAt,
        DateTimeOffset LastUpdated);

    public sealed record InboundShipmentDetailDto(
        int Id,
        string ReferenceNumber,
        int SupplierId,
        string SupplierName,
        DateOnly? ExpectedDate,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        IReadOnlyList<InboundShipmentLineDetailDto> Lines);

    public sealed record InboundShipmentLineDetailDto(
        int LineId,
        int ItemId,
        string ItemSku,
        string ItemName,
        string? PrimaryBarcode,
        string? PrimaryThumbnailUrl,
        bool RequiresLotTracking,
        bool RequiresQC,
        decimal ExpectedQty,
        decimal ReceivedQty,
        string BaseUoM);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
