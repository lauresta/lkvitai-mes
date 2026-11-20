using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using LKvitai.MES.Modules.Warehouse.Domain;
using System.Globalization;
using System.Text;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
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
    private readonly IWarehouseCodeProvider _warehouseCode;
    private string DefaultWarehouseId => _warehouseCode.Code;

    private readonly IBalanceGuardLockFactory _balanceLocks;
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly IMediator _mediator;
    private readonly ILogger<ReceivingController> _logger;
    private readonly IItemPriceSnapshotService _itemPriceSnapshots;
    private readonly PurchaseDocumentService _purchase;

    public ReceivingController(
        IWarehouseCodeProvider warehouseCode,
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        IMediator mediator,
        ILogger<ReceivingController> logger,
        IItemPriceSnapshotService itemPriceSnapshots, IBalanceGuardLockFactory balanceLocks,
        PurchaseDocumentService purchase)
    {
        _purchase = purchase;
        _warehouseCode = warehouseCode;
        _balanceLocks = balanceLocks;
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _mediator = mediator;
        _logger = logger;
        _itemPriceSnapshots = itemPriceSnapshots;
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
            var csvPrices = await LoadShipmentPriceTotalsAsync(
                csvRows.Select(x => x.ShipmentId).ToArray(), cancellationToken);

            return File(
                Encoding.UTF8.GetBytes(BuildShipmentsCsv(csvRows, csvPrices)),
                "text/csv",
                "receiving-history.csv");
        }

        var totalCount = await Marten.QueryableExtensions.CountAsync(query, cancellationToken);
        var items = await Marten.QueryableExtensions.ToListAsync(
            query.OrderByDescending(x => x.LastUpdated)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize),
            cancellationToken);
        var priceTotals = await LoadShipmentPriceTotalsAsync(
            items.Select(x => x.ShipmentId).ToArray(), cancellationToken);

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
                x.LastUpdated)
            {
                TotalExpectedValue = priceTotals.TryGetValue(x.ShipmentId, out var totals) ? totals.Purchase : null,
                TotalCostValue = priceTotals.TryGetValue(x.ShipmentId, out totals) ? totals.Cost : null,
                TotalSaleValue = priceTotals.TryGetValue(x.ShipmentId, out totals) ? totals.Sale : null
            }).ToList(),
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
                    x.Item?.CostType,
                    x.InvoiceQty ?? x.ExpectedQty,
                    x.InvoiceUnitPrice ?? x.UnitPrice,
                    x.FinalValueEur))
                .ToList(),
            shipment.AdditionalCosts
                .OrderBy(x => x.Id)
                .Select(x => new AdditionalCostDto(x.Id, x.CostType, x.Amount, x.Currency, x.AmountEur))
                .ToList(),
            await BuildPurchaseDtoAsync(shipment, cancellationToken)));
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
            request.SupplierId, request.Lines, request.AdditionalCosts, NormalizeCurrency(request.Currency), cancellationToken);
        if (inputs is null)
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

        var headerError = ApplyAccountingHeader(shipment, request);
        if (headerError is not null)
        {
            return headerError;
        }

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

        await _purchase.RefreshPreliminaryAsync(shipment, cancellationToken);
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

        // Blueprint 4.2: header, prices and additional costs stay editable until the document is posted,
        // also after the first receipt. A posted document is locked.
        if (shipment.PostingStatus == "Posted")
        {
            return UnprocessableFailure($"Shipment '{id}' is posted and locked.");
        }

        var (inputs, inputsError) = await LoadShipmentInputsAsync(
            request.SupplierId, request.Lines, request.AdditionalCosts, NormalizeCurrency(request.Currency), cancellationToken);
        if (inputs is null)
        {
            return inputsError ?? Failure(Result.Fail(DomainErrorCodes.InternalError, "Shipment input validation failed unexpectedly."));
        }

        var supplier = inputs.Supplier;

        shipment.ReferenceNumber = request.ReferenceNumber.Trim();
        shipment.SupplierId = request.SupplierId;
        shipment.ExpectedDate = request.ExpectedDate;
        shipment.InvoiceNumber = string.IsNullOrWhiteSpace(request.InvoiceNumber) ? null : request.InvoiceNumber.Trim();
        shipment.InvoiceDate = request.InvoiceDate;
        var headerError = ApplyAccountingHeader(shipment, request);
        if (headerError is not null)
        {
            return headerError;
        }

        // Lines are matched by item. A line that already received goods cannot be removed or cut below what was received.
        var requested = request.Lines.ToDictionary(x => x.ItemId);
        foreach (var existing in shipment.Lines.Where(x => !requested.ContainsKey(x.ItemId)).ToList())
        {
            if (existing.ReceivedQty > 0m && inputs.Items.First(i => i.Id == existing.ItemId).ItemType != ItemType.Service)
            {
                return UnprocessableFailure($"Line {existing.Id} already received goods and cannot be removed.");
            }

            _dbContext.Remove(existing);
            shipment.Lines.Remove(existing);
        }

        var newLines = BuildShipmentLines(request.Lines, inputs.Items);
        foreach (var fresh in newLines)
        {
            var existing = shipment.Lines.FirstOrDefault(x => x.ItemId == fresh.ItemId);
            if (existing is null)
            {
                shipment.Lines.Add(fresh);
                continue;
            }

            var isService = inputs.Items.First(i => i.Id == fresh.ItemId).ItemType == ItemType.Service;
            if (!isService && fresh.ExpectedQty < existing.ReceivedQty)
            {
                return UnprocessableFailure($"Line {existing.Id}: expected quantity cannot be below the received {existing.ReceivedQty:0.##}.");
            }

            existing.ExpectedQty = fresh.ExpectedQty;
            existing.ReceivedQty = isService ? fresh.ExpectedQty : existing.ReceivedQty;
            existing.UnitPrice = fresh.UnitPrice;
            existing.InvoiceQty = fresh.InvoiceQty;
            existing.InvoiceUnitPrice = fresh.InvoiceUnitPrice;
        }

        _dbContext.RemoveRange(shipment.AdditionalCosts);
        shipment.AdditionalCosts.Clear();
        foreach (var costRow in inputs.AdditionalCostRows)
        {
            shipment.AdditionalCosts.Add(costRow);
        }

        shipment.Status = ComputeShipmentStatus(shipment.Lines);
        await _purchase.RefreshPreliminaryAsync(shipment, cancellationToken);


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

    [HttpPost("{id:int}/close-short")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> CloseShortAsync(int id, [FromBody] CloseShortRequest request, CancellationToken cancellationToken = default)
    {
        await using var shipmentLock = await _balanceLocks.CreateAsync(cancellationToken);
        await shipmentLock.AcquireAsync([StockLockKey.ForLocation(DefaultWarehouseId, "INBOUND", id.ToString(CultureInfo.InvariantCulture))], cancellationToken);
        var shipment = await _dbContext.InboundShipments.Include(x => x.Lines).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (shipment is null) return Failure(Result.Fail(DomainErrorCodes.NotFound, "Shipment not found."));
        if (shipment.Status is "Complete" or "Cancelled") return ValidationFailure("Shipment is already closed.");
        if (!shipment.Lines.Any(x => x.ReceivedQty < x.ExpectedQty)) return ValidationFailure("Shipment has no shortage.");
        if (shipment.PostingStatus == "Posted") return UnprocessableFailure("The document is posted; a shortage after posting is handled with a credit note.");
        shipment.Status = "Complete";
        var evt = new InboundShipmentClosedEvent { ShipmentId = id, UserId = _currentUserService.GetCurrentUserId(),
            Notes = request.Notes, Timestamp = DateTime.UtcNow };
        await PersistShipmentEventAsync(id, evt, null, cancellationToken);
        return Ok(new ShipmentUpdatedResponse(id, shipment.ReferenceNumber, shipment.Status, evt.Timestamp));
    }

    private async Task<PurchaseDocumentDto> BuildPurchaseDtoAsync(InboundShipment shipment, CancellationToken ct)
    {
        IReadOnlyList<PostingProblemDto> problems = [];
        if (shipment.PostingStatus != "Posted")
        {
            try
            {
                problems = (await _purchase.DryRunAsync(shipment.Id, ct)).Select(p => new PostingProblemDto(p.Code, p.Text)).ToList();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Posting checks for shipment {ShipmentId} could not be evaluated.", shipment.Id);
            }
        }

        var authorization = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
        var canManual = (await authorization.AuthorizeAsync(User, WarehousePolicies.InventoryAccountantOrManager)).Succeeded;
        var queue = shipment.PostingStatus == "Posted" && shipment.AccountingRoute == "Auto"
            ? await _dbContext.AgnumOutbox.AsNoTracking().Where(r => r.DocumentType == "InboundShipment" && r.DocumentId == shipment.Id)
                .OrderByDescending(r => r.Id).FirstOrDefaultAsync(ct)
            : null;
        return new PurchaseDocumentDto(
            shipment.Currency, shipment.NetAmount, shipment.VatAmount, shipment.GrossAmount,
            shipment.RateUnitsPerEur, shipment.RateDate, shipment.RateSource, shipment.RateReason,
            shipment.NetEur, shipment.VatEur, shipment.GrossEur,
            shipment.VatTreatment?.ToString(),
            shipment.Supplier is null ? null : Infrastructure.Purchasing.PurchasePostingValidator.DefaultVatTreatment(shipment.Supplier.Kind)?.ToString(),
            shipment.AccountingRoute, shipment.PostingStatus, shipment.PostedAt, shipment.PostedBy, canManual,
            shipment.ManualReason, queue?.Status, queue?.Operation, queue?.LastError, queue?.Attempts ?? 0, problems,
            shipment.DeliveryTerms, shipment.TransportMode, shipment.DispatchCountry, shipment.TransactionNature);
    }

    /// <summary>Blueprint 4.2 "Post": runs every blocking check, then writes the true-up and locks the document.</summary>
    [HttpPost("{id:int}/post")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> PostShipmentAsync(
        int id, [FromBody] PostShipmentRequest? request, CancellationToken cancellationToken = default)
    {
        decimal? manualRate = null;
        string? manualReason = null;
        if (request?.ManualRateUnitsPerEur is > 0m)
        {
            var authorization = HttpContext.RequestServices.GetRequiredService<Microsoft.AspNetCore.Authorization.IAuthorizationService>();
            if (!(await authorization.AuthorizeAsync(User, WarehousePolicies.InventoryAccountantOrManager)).Succeeded)
            {
                return StatusCode(StatusCodes.Status403Forbidden);
            }

            manualRate = request.ManualRateUnitsPerEur;
            manualReason = request.ManualRateReason;
        }

        var result = await _purchase.PostAsync(id, manualRate, manualReason, _currentUserService.GetCurrentUserId(), cancellationToken);
        if (result.NotFound)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Shipment '{id}' does not exist."));
        }

        if (!result.Posted)
        {
            return UnprocessableFailure(string.Join("\n", result.Problems.Select(p => $"{p.Code}|{p.Text}")));
        }

        return await GetShipmentByIdAsync(id, cancellationToken);
    }

    public sealed record CloseShortRequest(string? Notes);

    private Task PersistShipmentEventAsync(int id, object evt, Action? afterEfSave, CancellationToken ct)
        => PersistShipmentEventAsync(id, evt, afterEfSave, null, ct);

    // Quantity event and value event commit in one transaction; a lost value-stream version race is retried.
    private async Task PersistShipmentEventAsync(
        int id, object evt, Action? afterEfSave, StockValueRequest? valueRequest, CancellationToken ct)
    {
        IReadOnlyList<StockValueChanged> valueEvents = [];
        await StockValueRetry.RunAsync(() => _dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            await using var session = _documentStore.LightweightSession(Marten.Services.SessionOptions.ForTransaction(
                (NpgsqlTransaction)transaction.GetDbTransaction(), false));
            await _dbContext.SaveChangesAsync(acceptAllChangesOnSuccess: false, ct);
            afterEfSave?.Invoke();
            session.Events.Append(ShipmentStreamId(id), evt);
            valueEvents = await StockValueWriter.AppendAsync(session, _dbContext, [valueRequest], ct);
            await session.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _dbContext.ChangeTracker.AcceptAllChanges();
        }), ct);
        await StockValueWriter.ProjectAsync(_dbContext, _logger, valueEvents, ct);
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

        await using var shipmentLock = await _balanceLocks.CreateAsync(cancellationToken);
        await shipmentLock.AcquireAsync([StockLockKey.ForLocation(DefaultWarehouseId, "INBOUND", id.ToString(CultureInfo.InvariantCulture))], cancellationToken);

        var shipment = await _dbContext.InboundShipments
            .Include(x => x.Lines)
                .ThenInclude(x => x.Item)
            .Include(x => x.AdditionalCosts)
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (shipment is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Shipment '{id}' does not exist."));
        }

        if (shipment.Status is "Complete" or "Cancelled")
            return ValidationFailure("Shipment is closed and cannot receive more stock.");

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

        // Blueprint 4.2: before posting a receipt is qty x receipt price x rate (preliminary rate: invoice date, else
        // receipt date); after posting it follows the final value of the line, so the line sum stays exact.
        var invoiceQty = line.InvoiceQty ?? line.ExpectedQty;
        decimal receiptValueEur;
        decimal receiptRateEurPerUnit;
        if (shipment.PostingStatus == "Posted" && line.FinalValueEur is not null)
        {
            receiptRateEurPerUnit = PurchaseMath.RateEurPerUnit(shipment.RateUnitsPerEur ?? 1m);
            receiptValueEur = PurchaseMath.ReceivedPortion(line.FinalValueEur.Value, line.ReceivedQty + request.ReceivedQty, invoiceQty)
                              - line.BookedValueEur;
        }
        else
        {
            var rate = await _purchase.ResolveRateAsync(
                shipment.Currency, shipment.InvoiceDate ?? PurchaseDocumentService.Today(), null, null, cancellationToken);
            if (rate.UnitsPerEur is null)
            {
                return UnprocessableFailure($"There is no exchange rate for {shipment.Currency} yet; the receipt cannot be valued.");
            }

            receiptRateEurPerUnit = PurchaseMath.RateEurPerUnit(rate.UnitsPerEur.Value);
            receiptValueEur = PurchaseMath.ToEur(request.ReceivedQty * effectiveUnitPrice.Value, receiptRateEurPerUnit);
        }

        line.BookedValueEur += receiptValueEur;
        _dbContext.InboundShipmentReceipts.Add(new InboundShipmentReceipt
        {
            ShipmentId = shipment.Id,
            LineId = line.Id,
            Qty = request.ReceivedQty,
            UnitPrice = effectiveUnitPrice.Value,
            RateEurPerUnit = receiptRateEurPerUnit,
            ValueEur = receiptValueEur,
            ReceivedAt = DateTimeOffset.UtcNow,
            ReceivedBy = _currentUserService.GetCurrentUserId()
        });

        line.ReceivedQty += request.ReceivedQty;

        shipment.Status = ComputeShipmentStatus(shipment.Lines);

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

        // The receipt value (qty x price, EUR) is written in the same transaction as the quantity event.
        var receiptDocument = new StockValueDocument(
            "InboundShipment",
            shipment.Id.ToString(CultureInfo.InvariantCulture),
            shipment.ReferenceNumber,
            string.Format(
                CultureInfo.InvariantCulture,
                "Receipt {0} - {1:0.###} {2} @ {3:0.####}",
                shipment.ReferenceNumber, request.ReceivedQty, line.BaseUoM, effectiveUnitPrice.Value));
        await PersistShipmentEventAsync(
            shipment.Id, evt, () => evt.LotId = lot?.Id,
            StockValueRequest.ForReceipt(item.InternalSKU, request.ReceivedQty, receiptValueEur, receiptDocument),
            cancellationToken);

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
    /// Runs the supplier/items/additional-costs validation pipeline shared by
    /// CreateShipmentAsync and UpdateShipmentAsync - both need the exact same three checks
    /// before touching the shipment entity. Returns null Inputs with the first failure as
    /// Error; callers only need one guard clause instead of three.
    /// </summary>
    private async Task<(ShipmentInputs? Inputs, ObjectResult? Error)> LoadShipmentInputsAsync(
        int supplierId,
        IReadOnlyList<CreateInboundShipmentLineRequest> lines,
        IReadOnlyList<CreateAdditionalCostRequest>? additionalCosts,
        string currency,
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

        var (additionalCostRows, costError) = BuildAdditionalCostRows(additionalCosts, currency);
        if (costError is not null)
        {
            return (null, costError);
        }

        // Blueprint 4.4: cost types come from the dictionary; "VAT" is not a cost type.
        var codes = additionalCostRows.Select(x => x.CostType).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (codes.Any(x => string.Equals(x, "VAT", StringComparison.OrdinalIgnoreCase)))
        {
            return (null, ValidationFailure("VAT is not an additional cost type; enter it in the VAT amount."));
        }

        if (codes.Count > 0)
        {
            var known = await _dbContext.AdditionalCostTypes.AsNoTracking()
                .Where(x => x.IsActive).Select(x => x.Code).ToListAsync(cancellationToken);
            var unknown = codes.Where(c => !known.Contains(c, StringComparer.OrdinalIgnoreCase)).ToList();
            if (unknown.Count > 0)
            {
                return (null, ValidationFailure($"Unknown additional cost type: {string.Join(", ", unknown)}."));
            }

            foreach (var row in additionalCostRows)
            {
                row.CostType = known.First(k => string.Equals(k, row.CostType, StringComparison.OrdinalIgnoreCase));
            }
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
                InvoiceQty = line.InvoiceQty,
                InvoiceUnitPrice = line.InvoiceUnitPrice
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
        IReadOnlyList<CreateAdditionalCostRequest>? requested, string currency)
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
                Currency = currency
            })
            .ToList();

        return (rows, null);
    }

    private static string? NormalizeUpper(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private static string NormalizeCurrency(string? currency)
        => string.IsNullOrWhiteSpace(currency) ? "EUR" : currency.Trim().ToUpperInvariant();

    /// <summary>Blueprint 4.2 header fields; amounts are entered from the invoice, gross is net + VAT.</summary>
    private ObjectResult? ApplyAccountingHeader(InboundShipment shipment, CreateInboundShipmentRequest request)
    {
        var currency = NormalizeCurrency(request.Currency);
        if (currency.Length != 3 || !currency.All(char.IsLetter))
        {
            return ValidationFailure("Field 'currency' must be a 3-letter ISO code.");
        }

        if (request.NetAmount is < 0m || request.VatAmount is < 0m)
        {
            return ValidationFailure("Net and VAT amounts must be greater than or equal to 0.");
        }

        VatTreatment? treatment = null;
        if (!string.IsNullOrWhiteSpace(request.VatTreatment))
        {
            if (!Enum.TryParse<VatTreatment>(request.VatTreatment, true, out var parsed))
            {
                return ValidationFailure("Field 'vatTreatment' must be Domestic, EuAcquisition or Import.");
            }

            treatment = parsed;
        }

        var route = string.IsNullOrWhiteSpace(request.AccountingRoute) ? "Auto" : request.AccountingRoute.Trim();
        if (route is not ("Auto" or "ManualInAgnum"))
        {
            return ValidationFailure("Field 'accountingRoute' must be Auto or ManualInAgnum.");
        }

        shipment.Currency = currency;
        shipment.NetAmount = request.NetAmount;
        shipment.VatAmount = request.VatAmount;
        shipment.GrossAmount = request.NetAmount is null ? null : request.NetAmount + (request.VatAmount ?? 0m);
        shipment.VatTreatment = treatment;
        shipment.AccountingRoute = route;
        shipment.ManualReason = route == "ManualInAgnum" ? request.ManualReason?.Trim() : null;
        shipment.DeliveryTerms = NormalizeUpper(request.DeliveryTerms);
        shipment.TransportMode = NormalizeUpper(request.TransportMode);
        shipment.DispatchCountry = NormalizeUpper(request.DispatchCountry);
        shipment.TransactionNature = NormalizeUpper(request.TransactionNature) ?? "11";
        return null;
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

    private async Task<Dictionary<int, ShipmentPriceTotals>> LoadShipmentPriceTotalsAsync(
        int[] shipmentIds, CancellationToken cancellationToken)
    {
        if (shipmentIds.Length == 0) return [];

        var lines = await _dbContext.InboundShipmentLines.AsNoTracking()
            .Where(x => shipmentIds.Contains(x.ShipmentId))
            .Select(x => new { x.ShipmentId, x.ItemId, x.ExpectedQty, x.UnitPrice })
            .ToListAsync(cancellationToken);
        var snapshots = await _itemPriceSnapshots.GetAsync(
            lines.Select(x => x.ItemId).Distinct().ToArray(), Array.Empty<string>(), cancellationToken);
        var byId = snapshots.ToDictionary(x => x.ItemId);

        return lines.GroupBy(x => x.ShipmentId).ToDictionary(x => x.Key, group =>
        {
            var purchase = group.All(x => x.UnitPrice.HasValue)
                ? group.Sum(x => x.ExpectedQty * x.UnitPrice!.Value) : (decimal?)null;
            var cost = group.All(x => byId.TryGetValue(x.ItemId, out var price) && price.EffectiveCost.HasValue)
                ? group.Sum(x => ItemPriceResolver.Value(x.ExpectedQty, byId[x.ItemId].EffectiveCost)!.Value) : (decimal?)null;
            var sale = group.All(x => byId.TryGetValue(x.ItemId, out var price) && price.BasePrice.HasValue)
                ? group.Sum(x => ItemPriceResolver.Value(x.ExpectedQty, byId[x.ItemId].BasePrice)!.Value) : (decimal?)null;
            return new ShipmentPriceTotals(purchase, cost, sale);
        });
    }

    private sealed record ShipmentPriceTotals(decimal? Purchase, decimal? Cost, decimal? Sale);

    private static string BuildShipmentsCsv(IReadOnlyCollection<InboundShipmentSummaryView> rows,
        IReadOnlyDictionary<int, ShipmentPriceTotals> prices)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ShipmentId,ReferenceNumber,SupplierId,SupplierName,ExpectedDate,Status,TotalLines,TotalExpectedQty,TotalReceivedQty,PurchaseTotal,CostTotal,SaleTotal,CreatedAt,LastUpdated");

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
            prices.TryGetValue(row.ShipmentId, out var totals);
            sb.Append(totals?.Purchase?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(totals?.Cost?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
            sb.Append(totals?.Sale?.ToString(CultureInfo.InvariantCulture) ?? string.Empty).Append(',');
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
        IReadOnlyList<CreateAdditionalCostRequest>? AdditionalCosts = null,
        string? Currency = null,
        decimal? NetAmount = null,
        decimal? VatAmount = null,
        string? VatTreatment = null,
        string? AccountingRoute = null,
        string? ManualReason = null,
        string? DeliveryTerms = null,
        string? TransportMode = null,
        string? DispatchCountry = null,
        string? TransactionNature = null);

    public sealed record CreateInboundShipmentLineRequest(
        int ItemId,
        decimal ExpectedQty,
        decimal? UnitPrice,
        string? Currency,
        decimal? InvoiceQty = null,
        decimal? InvoiceUnitPrice = null);

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
        DateTimeOffset LastUpdated)
    {
        public decimal? TotalExpectedValue { get; init; }
        public decimal? TotalCostValue { get; init; }
        public decimal? TotalSaleValue { get; init; }
    }

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
        IReadOnlyList<AdditionalCostDto> AdditionalCosts,
        PurchaseDocumentDto? Purchase = null);

    public sealed record PostingProblemDto(string Code, string Text);

    public sealed record PurchaseDocumentDto(
        string Currency,
        decimal? NetAmount,
        decimal? VatAmount,
        decimal? GrossAmount,
        decimal? RateUnitsPerEur,
        DateOnly? RateDate,
        string? RateSource,
        string? RateReason,
        decimal? NetEur,
        decimal? VatEur,
        decimal? GrossEur,
        string? VatTreatment,
        string? DefaultVatTreatment,
        string AccountingRoute,
        string PostingStatus,
        DateTimeOffset? PostedAt,
        string? PostedBy,
        bool CanSetManualRate,
        string? ManualReason,
        string? QueueStatus,
        string? QueueOperation,
        string? QueueError,
        int QueueAttempts,
        IReadOnlyList<PostingProblemDto> Problems,
        string? DeliveryTerms = null,
        string? TransportMode = null,
        string? DispatchCountry = null,
        string? TransactionNature = null);

    public sealed record PostShipmentRequest(decimal? ManualRateUnitsPerEur, string? ManualRateReason);

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
        string? CostType,
        decimal? InvoiceQty = null,
        decimal? InvoiceUnitPrice = null,
        decimal? FinalValueEur = null);

    public sealed record AdditionalCostDto(
        int Id,
        string CostType,
        decimal Amount,
        string Currency,
        decimal? AmountEur = null);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
