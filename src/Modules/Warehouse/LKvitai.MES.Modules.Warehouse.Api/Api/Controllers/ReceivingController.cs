using System.Diagnostics;
using System.Globalization;
using System.Text;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using IDocumentStore = Marten.IDocumentStore;
using MediatR;
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

    /// <summary>
    /// Marker passed as ApprovedBy when the receiving flow auto-adjusts an item's
    /// weighted-average cost from a real purchase. This is real purchase data derived
    /// from a receipt, not a manual override, so it bypasses the &gt;20% manual-approval
    /// gate that exists to catch typos in ad-hoc cost adjustments (see
    /// ValuationCostAdjustmentPolicy.ValidateApproval).
    /// </summary>
    private const string ReceivingValuationActor = "system:receiving";

    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;
    private readonly ILogger<ReceivingController> _logger;

    public ReceivingController(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        IMediator mediator,
        ILogger<ReceivingController> logger)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _mediator = mediator;
        _logger = logger;
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
            .Include(x => x.AdditionalCosts)
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
            shipment.InvoiceNumber,
            shipment.InvoiceDate,
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
                    x.BaseUoM,
                    x.UnitPrice,
                    x.Currency,
                    (x.Item?.ItemType ?? ItemType.Stock).ToString(),
                    x.Item?.CostType))
                .ToList(),
            shipment.AdditionalCosts
                .OrderBy(x => x.Id)
                .Select(x => new AdditionalCostDto(x.Id, x.CostType, x.Amount, x.Currency))
                .ToList()));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> CreateShipmentAsync(
        [FromBody] CreateInboundShipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var shapeError = ValidateShipmentRequestShape(request);
        if (shapeError is not null)
        {
            return shapeError;
        }

        var (inputs, inputsError) = await LoadShipmentInputsAsync(
            request.SupplierId, request.Lines, request.AdditionalCosts, cancellationToken);
        if (inputs is null || inputsError is null)
        {
            return inputsError ?? Failure(Result.Fail(DomainErrorCodes.InternalError, "Shipment input validation failed unexpectedly."));
        }

        var supplier = inputs.Supplier;

        var shipment = new InboundShipment
        {
            ReferenceNumber = request.ReferenceNumber.Trim(),
            SupplierId = request.SupplierId,
            ExpectedDate = request.ExpectedDate,
            Status = "Draft",
            InvoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? null : request.InvoiceNumber.Trim(),
            InvoiceDate = request.InvoiceDate
        };

        foreach (var costRow in inputs.AdditionalCostRows)
        {
            shipment.AdditionalCosts.Add(costRow);
        }

        // Service-type lines (e.g. "Transport" invoiced as its own line) have nothing
        // physical to receive - auto-complete them at creation instead of routing them
        // through ReceiveGoodsAsync's lot/QC/location logic (see plan decision #3).
        foreach (var line in BuildShipmentLines(request.Lines, inputs.Items))
        {
            shipment.Lines.Add(line);
        }

        shipment.Status = ComputeShipmentStatus(shipment.Lines);

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

        try
        {
            await using var session = _documentStore.LightweightSession();

            // StartStream (not Append) is deliberate: this must be the first event on this
            // shipment's stream. If a stream already exists for this id - e.g. a stale event
            // stream left behind by a prior reset of the `inbound_shipments` table that didn't
            // also clear the Marten event store - Append would silently land this event on top
            // of unrelated history, and the InboundShipmentSummaryProjection (which only runs
            // its Create handler on a stream's first matching event) would keep showing the old
            // ghost data forever while this brand-new shipment never appears correctly in the
            // list. Fail loudly instead so the id collision gets noticed and cleaned up.
            session.Events.StartStream(ShipmentStreamId(shipment.Id), evt);
            await session.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(
                ex,
                "Shipment {ShipmentId} ({ReferenceNumber}) was created in the database but its event " +
                "stream could not be started - a stream for this id may already exist (stale data from a " +
                "prior partial reset). The shipment record exists but will not appear correctly in the " +
                "inbound-shipments list until this is investigated and the conflicting stream is cleaned up.",
                shipment.Id,
                shipment.ReferenceNumber);

            return Failure(Result.Fail(
                DomainErrorCodes.InternalError,
                $"Shipment '{shipment.Id}' was created but its event history could not be initialized " +
                "due to a data conflict. Contact an administrator before relying on this shipment - it may " +
                "not appear correctly in the shipments list."));
        }

        return Created(
            $"/api/warehouse/v1/receiving/shipments/{shipment.Id}",
            new ShipmentCreatedResponse(
                shipment.Id,
                shipment.ReferenceNumber,
                shipment.Status,
                now));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> UpdateShipmentAsync(
        int id,
        [FromBody] CreateInboundShipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        var shapeError = ValidateShipmentRequestShape(request);
        if (shapeError is not null)
        {
            return shapeError;
        }

        var shipment = await _dbContext.InboundShipments
            .Include(x => x.Lines)
            .Include(x => x.AdditionalCosts)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shipment is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Shipment '{id}' does not exist."));
        }

        // Editing is only safe before any receiving has happened - once a line has a
        // received qty, changing/removing it here would silently orphan that stock
        // movement from what the shipment claims it received. ComputeShipmentStatus
        // only reports "Draft" when every line's ReceivedQty is still 0.
        if (shipment.Status != "Draft")
        {
            return UnprocessableFailure(
                $"Shipment '{id}' has status '{shipment.Status}' and can no longer be edited - " +
                "receiving has already started against it.");
        }

        var (inputs, inputsError) = await LoadShipmentInputsAsync(
            request.SupplierId, request.Lines, request.AdditionalCosts, cancellationToken);
        if (inputs is null || inputsError is null)
        {
            return inputsError ?? Failure(Result.Fail(DomainErrorCodes.InternalError, "Shipment input validation failed unexpectedly."));
        }

        var supplier = inputs.Supplier;

        shipment.ReferenceNumber = request.ReferenceNumber.Trim();
        shipment.SupplierId = request.SupplierId;
        shipment.ExpectedDate = request.ExpectedDate;
        shipment.InvoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? null : request.InvoiceNumber.Trim();
        shipment.InvoiceDate = request.InvoiceDate;

        // No line has been received yet (guarded above), so the old rows can be fully
        // replaced rather than diffed line-by-line.
        _dbContext.RemoveRange(shipment.Lines);
        shipment.Lines.Clear();
        _dbContext.RemoveRange(shipment.AdditionalCosts);
        shipment.AdditionalCosts.Clear();

        foreach (var costRow in inputs.AdditionalCostRows)
        {
            shipment.AdditionalCosts.Add(costRow);
        }

        foreach (var line in BuildShipmentLines(request.Lines, inputs.Items))
        {
            shipment.Lines.Add(line);
        }

        shipment.Status = ComputeShipmentStatus(shipment.Lines);

        await _dbContext.SaveChangesAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var evt = new InboundShipmentUpdatedEvent
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

        // Belt-and-braces: the async InboundShipmentSummaryProjection daemon will also pick
        // this event up on its own, but its catch-up timing is not something a user editing
        // a shipment should have to wait on (or risk missing, if the daemon's batch happens
        // to skip a stale-then-updated document - observed during manual testing). Patch the
        // list's read model directly and immediately so the edit is reflected right away
        // regardless of daemon timing; the later async replay of the same event is a no-op
        // since it recomputes the identical values.
        await using (var patchSession = _documentStore.LightweightSession())
        {
            // SingleStreamProjection identifies its document by the event STREAM id, not by
            // whatever the Create() handler assigns to InboundShipmentSummaryView.Id -
            // confirmed by inspecting the persisted rows (id column = "inbound-shipment:{n}",
            // matching ShipmentStreamId, not InboundShipmentSummaryView.ComputeId's plain "{n}").
            var summaryView = await patchSession.LoadAsync<InboundShipmentSummaryView>(
                ShipmentStreamId(shipment.Id), cancellationToken);

            if (summaryView is not null)
            {
                summaryView.ReferenceNumber = shipment.ReferenceNumber;
                summaryView.SupplierId = shipment.SupplierId;
                summaryView.SupplierName = supplier.Name;
                summaryView.ExpectedDate = shipment.ExpectedDate;
                summaryView.TotalLines = shipment.Lines.Count;
                summaryView.TotalExpectedQty = shipment.Lines.Sum(x => x.ExpectedQty);
                summaryView.CompletionPercent = summaryView.TotalExpectedQty <= 0m
                    ? 0m
                    : Math.Min(1m, summaryView.TotalReceivedQty / summaryView.TotalExpectedQty);
                summaryView.LastUpdated = now;

                patchSession.Store(summaryView);
                await patchSession.SaveChangesAsync(cancellationToken);
            }
        }

        return Ok(new ShipmentUpdatedResponse(shipment.Id, shipment.ReferenceNumber, shipment.Status, now));
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
                .ThenInclude(x => x.Item)
            .Include(x => x.AdditionalCosts)
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

        if (request.UnitPrice is < 0m)
        {
            return ValidationFailure("Field 'unitPrice' must be greater than or equal to 0.");
        }

        var effectiveUnitPrice = request.UnitPrice ?? line.UnitPrice;
        if (effectiveUnitPrice is null)
        {
            return UnprocessableFailure(
                $"Unit price is required before receiving line '{line.Id}'. Set it on the shipment line or provide it with this request.");
        }

        if (request.UnitPrice.HasValue)
        {
            line.UnitPrice = request.UnitPrice.Value;
            if (!string.IsNullOrWhiteSpace(request.Currency))
            {
                line.Currency = request.Currency.Trim().ToUpperInvariant();
            }
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

        shipment.Status = ComputeShipmentStatus(shipment.Lines);

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

        // Best-effort: fold this receipt's price (plus its proportional share of the
        // shipment's header-level landed costs) into the item's weighted-average cost.
        // The physical receipt above has already succeeded and must not be rolled back
        // or fail the request because of a valuation hiccup — see Non-Negotiable
        // Constraints (valuation is a separate concern from physical stock).
        try
        {
            await UpdateItemValuationForReceiptAsync(
                shipment,
                line,
                item.Id,
                request.ReceivedQty,
                effectiveUnitPrice.Value,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to update valuation for item {ItemId} after receiving shipment line {LineId}. Physical receipt was still recorded.",
                item.Id,
                line.Id);
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

    /// <summary>
    /// Computes this receipt's all-in landed unit cost (invoice price plus its proportional
    /// share of the shipment's additional-cost pool - manual <see cref="InboundShipmentAdditionalCost"/>
    /// rows plus any Service-type sibling lines, allocated across Stock-type lines by expected
    /// value) and folds it into the item's <see cref="ItemValuation"/> stream — initializing it
    /// on the first receipt, or recomputing a weighted average against the current on-hand cost
    /// afterwards.
    /// </summary>
    private async Task UpdateItemValuationForReceiptAsync(
        InboundShipment shipment,
        InboundShipmentLine line,
        int itemId,
        decimal receivedQty,
        decimal unitPrice,
        CancellationToken cancellationToken)
    {
        var totalAdditionalCost = ComputeTotalAdditionalCostPool(shipment);

        var landedUnitCost = unitPrice;
        if (totalAdditionalCost > 0m)
        {
            var totalExpectedValue = shipment.Lines
                .Where(x => x.Item is null || x.Item.ItemType == ItemType.Stock)
                .Sum(x => x.ExpectedQty * (x.UnitPrice ?? 0m));
            if (totalExpectedValue > 0m && line.ExpectedQty > 0m)
            {
                var lineExpectedValue = line.ExpectedQty * unitPrice;
                var lineShareOfAdditionalCost = totalAdditionalCost * lineExpectedValue / totalExpectedValue;
                landedUnitCost += lineShareOfAdditionalCost / line.ExpectedQty;
            }
        }

        var currency = string.IsNullOrWhiteSpace(line.Currency) ? "EUR" : line.Currency;
        var reason = string.Format(
            CultureInfo.InvariantCulture,
            "Receipt {0} - {1:0.###} {2} @ {3:0.####} {4}",
            shipment.ReferenceNumber,
            receivedQty,
            line.BaseUoM,
            landedUnitCost,
            currency);

        var initializeResult = await _mediator.Send(
            new InitializeValuationCommand
            {
                ItemId = itemId,
                InitialCost = landedUnitCost,
                Reason = reason
            },
            cancellationToken);

        if (initializeResult.IsSuccess)
        {
            return;
        }

        var existingValuation = await _dbContext.OnHandValues
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ItemId == itemId, cancellationToken);

        var newCost = landedUnitCost;
        if (existingValuation is not null && existingValuation.Qty > 0m)
        {
            newCost = ((existingValuation.Qty * existingValuation.UnitCost) + (receivedQty * landedUnitCost))
                / (existingValuation.Qty + receivedQty);
        }

        var adjustResult = await _mediator.Send(
            new AdjustValuationCostCommand
            {
                ItemId = itemId,
                NewCost = newCost,
                Reason = reason,
                ApprovedBy = ReceivingValuationActor
            },
            cancellationToken);

        if (!adjustResult.IsSuccess)
        {
            _logger.LogWarning(
                "Valuation adjust command failed for item {ItemId} after receipt: {Error}",
                itemId,
                adjustResult.Error);
        }
    }

    /// <summary>
    /// Runs the supplier/items/additional-costs validation pipeline shared by
    /// CreateShipmentAsync and UpdateShipmentAsync - both need the exact same three checks
    /// before touching the shipment entity. Returns null Inputs with the first failure as
    /// Error; callers only need one guard clause instead of three.
    /// </summary>
    private async Task<(ShipmentInputs? Inputs, ObjectResult? Error)> LoadShipmentInputsAsync(
        int supplierId,
        IReadOnlyList<CreateInboundShipmentLineRequest> lines,
        IReadOnlyList<CreateAdditionalCostRequest>? additionalCosts,
        CancellationToken cancellationToken)
    {
        var (supplier, supplierError) = await LoadSupplierAsync(supplierId, cancellationToken);
        if (supplierError is not null || supplier is null)
        {
            return (null, supplierError ?? Failure(Result.Fail(DomainErrorCodes.InternalError, "Supplier lookup failed unexpectedly.")));
        }

        var (items, itemsError) = await LoadLineItemsAsync(lines, cancellationToken);
        if (itemsError is not null)
        {
            return (null, itemsError);
        }

        var (additionalCostRows, costError) = BuildAdditionalCostRows(additionalCosts);
        if (costError is not null)
        {
            return (null, costError);
        }

        return (new ShipmentInputs(supplier, items, additionalCostRows), null);
    }

    private sealed record ShipmentInputs(
        Supplier Supplier,
        List<LineItemLookup> Items,
        List<InboundShipmentAdditionalCost> AdditionalCostRows);

    private async Task<(Supplier? Supplier, ObjectResult? Error)> LoadSupplierAsync(
        int supplierId,
        CancellationToken cancellationToken)
    {
        var supplier = await _dbContext.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == supplierId, cancellationToken);

        return supplier is null
            ? (null, ValidationFailure($"Supplier '{supplierId}' does not exist."))
            : (supplier, null);
    }

    private async Task<(List<LineItemLookup> Items, ObjectResult? Error)> LoadLineItemsAsync(
        IReadOnlyList<CreateInboundShipmentLineRequest> lines,
        CancellationToken cancellationToken)
    {
        var lineItemIds = lines.Select(x => x.ItemId).Distinct().ToList();
        var items = await _dbContext.Items
            .AsNoTracking()
            .Where(x => lineItemIds.Contains(x.Id))
            .Select(x => new LineItemLookup(x.Id, x.BaseUoM, x.ItemType))
            .ToListAsync(cancellationToken);

        return items.Count != lineItemIds.Count
            ? ([], ValidationFailure("One or more line ItemId values do not exist."))
            : (items, null);
    }

    /// <summary>
    /// Materializes shipment line entities from the request. Service-type lines (e.g. a
    /// "Transport" invoice line) have nothing physical to receive - auto-complete them here
    /// instead of routing them through ReceiveGoodsAsync's lot/QC/location logic (plan decision #3).
    /// Shared by create and update - both fully (re)build the line set from scratch.
    /// </summary>
    private static List<InboundShipmentLine> BuildShipmentLines(
        IReadOnlyList<CreateInboundShipmentLineRequest> requestLines,
        IReadOnlyList<LineItemLookup> items)
    {
        var lines = new List<InboundShipmentLine>();

        foreach (var line in requestLines)
        {
            var item = items.First(x => x.Id == line.ItemId);
            var isService = item.ItemType == ItemType.Service;

            lines.Add(new InboundShipmentLine
            {
                ItemId = line.ItemId,
                ExpectedQty = line.ExpectedQty,
                ReceivedQty = isService ? line.ExpectedQty : 0m,
                BaseUoM = item.BaseUoM,
                UnitPrice = line.UnitPrice,
                Currency = string.IsNullOrWhiteSpace(line.Currency) ? null : line.Currency.Trim().ToUpperInvariant()
            });
        }

        return lines;
    }

    private sealed record LineItemLookup(int Id, string BaseUoM, ItemType ItemType);

    private ObjectResult? ValidateShipmentRequestShape(CreateInboundShipmentRequest request)
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

        if (request.Lines.Any(x => x.ExpectedQty <= 0m))
        {
            return ValidationFailure("All shipment line expectedQty values must be greater than 0.");
        }

        if (request.Lines.Any(x => x.UnitPrice is < 0m))
        {
            return ValidationFailure("Shipment line unitPrice must be greater than or equal to 0.");
        }

        return null;
    }

    /// <summary>
    /// Validates and materializes the additional-cost rows for a new shipment. 0-amount rows
    /// are seeded defaults the user left untouched (see plan decision #1) and are silently
    /// dropped rather than persisted as noise.
    /// </summary>
    private (List<InboundShipmentAdditionalCost> Rows, ObjectResult? Error) BuildAdditionalCostRows(
        IReadOnlyList<CreateAdditionalCostRequest>? requested)
    {
        var requestedCosts = requested ?? Array.Empty<CreateAdditionalCostRequest>();
        if (requestedCosts.Any(x => x.Amount < 0m))
        {
            return ([], ValidationFailure("Shipment additional costs must be greater than or equal to 0."));
        }

        var positiveRows = requestedCosts.Where(x => x.Amount > 0m).ToList();
        if (positiveRows.Any(x => string.IsNullOrWhiteSpace(x.CostType)))
        {
            return ([], ValidationFailure("Each additional cost row with a positive amount must have a costType."));
        }

        var rows = positiveRows
            .Select(x => new InboundShipmentAdditionalCost
            {
                CostType = x.CostType.Trim(),
                Amount = x.Amount,
                Currency = string.IsNullOrWhiteSpace(x.Currency) ? "EUR" : x.Currency.Trim().ToUpperInvariant()
            })
            .ToList();

        return (rows, null);
    }

    private static string ComputeShipmentStatus(IEnumerable<InboundShipmentLine> lines)
    {
        var materialized = lines as ICollection<InboundShipmentLine> ?? lines.ToList();
        if (materialized.All(x => x.ReceivedQty >= x.ExpectedQty))
        {
            return "Complete";
        }

        return materialized.Any(x => x.ReceivedQty > 0m) ? "Partial" : "Draft";
    }

    /// <summary>
    /// Sums the shipment's manual <see cref="InboundShipmentAdditionalCost"/> rows plus the
    /// expected value of its Service-type lines (e.g. a "Transport" invoice line) - both feed
    /// the same landed-cost allocation pool distributed across Stock-type lines. No currency
    /// conversion is performed; amounts are summed as-is, same as the header-column model this
    /// replaced.
    /// </summary>
    private static decimal ComputeTotalAdditionalCostPool(InboundShipment shipment)
    {
        var manualCosts = shipment.AdditionalCosts.Sum(x => x.Amount);
        var serviceLineCosts = shipment.Lines
            .Where(x => x.Item is not null && x.Item.ItemType == ItemType.Service)
            .Sum(x => x.ExpectedQty * (x.UnitPrice ?? 0m));

        return manualCosts + serviceLineCosts;
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
        string? InvoiceNumber,
        DateOnly? InvoiceDate,
        IReadOnlyList<CreateInboundShipmentLineRequest> Lines,
        IReadOnlyList<CreateAdditionalCostRequest>? AdditionalCosts = null);

    public sealed record CreateInboundShipmentLineRequest(
        int ItemId,
        decimal ExpectedQty,
        decimal? UnitPrice,
        string? Currency);

    public sealed record CreateAdditionalCostRequest(
        string CostType,
        decimal Amount,
        string? Currency);

    public sealed record ShipmentCreatedResponse(int Id, string ReferenceNumber, string Status, DateTime CreatedAt);

    public sealed record ShipmentUpdatedResponse(int Id, string ReferenceNumber, string Status, DateTime UpdatedAt);

    public sealed record ReceiveShipmentLineRequest(
        int LineId,
        decimal ReceivedQty,
        string? LotNumber,
        DateOnly? ProductionDate,
        DateOnly? ExpiryDate,
        string? Notes,
        decimal? UnitPrice,
        string? Currency);

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
        string? InvoiceNumber,
        DateOnly? InvoiceDate,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        IReadOnlyList<InboundShipmentLineDetailDto> Lines,
        IReadOnlyList<AdditionalCostDto> AdditionalCosts);

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
        string BaseUoM,
        decimal? UnitPrice,
        string? Currency,
        string ItemType,
        string? CostType);

    public sealed record AdditionalCostDto(
        int Id,
        string CostType,
        decimal Amount,
        string Currency);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
