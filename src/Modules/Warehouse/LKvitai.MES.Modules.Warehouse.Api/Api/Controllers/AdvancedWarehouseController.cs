using LKvitai.MES.BuildingBlocks.ObjectStorage;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[WarehouseOperationValidation]
[Route("api/warehouse/v1/waves")]
public sealed class WavesController : ControllerBase
{
    private readonly IAdvancedWarehouseStore _store;

    public WavesController(IAdvancedWarehouseStore store)
    {
        _store = store;
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public IActionResult Create([FromBody] CreateWaveRequest request)
    {
        var orderIds = request.OrderIds?.Where(x => x != Guid.Empty).Distinct().Take(10).ToArray() ?? Array.Empty<Guid>();
        if (orderIds.Length == 0)
        {
            return BadRequest(new { message = "At least one valid orderId is required." });
        }

        var wave = _store.CreateWave(orderIds, request.AssignedOperator);
        return Ok(ToWaveResponse(wave));
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult List([FromQuery] string? status)
    {
        var items = _store.GetWaves(status).Select(ToWaveResponse);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult Get(Guid id)
    {
        var wave = _store.GetWave(id);
        return wave is null ? NotFound() : Ok(ToWaveResponse(wave));
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public IActionResult Assign(Guid id, [FromBody] AssignWaveRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.AssignedOperator))
        {
            return BadRequest(new { message = "assignedOperator is required." });
        }

        var wave = _store.AssignWave(id, request.AssignedOperator);
        return wave is null ? NotFound() : Ok(ToWaveResponse(wave));
    }

    [HttpPost("{id:guid}/start")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult Start(Guid id)
    {
        var wave = _store.StartWave(id);
        return wave is null ? NotFound() : Ok(ToWaveResponse(wave));
    }

    private static WaveResponse ToWaveResponse(WaveRecord wave)
    {
        return new WaveResponse(
            wave.Id,
            wave.WaveNumber,
            wave.Status.ToString().ToUpperInvariant(),
            wave.CreatedAt,
            wave.AssignedAt,
            wave.CompletedAt,
            wave.AssignedOperator,
            wave.OrderIds,
            wave.TotalLines,
            wave.CompletedLines,
            wave.PickList.Select(x => new WavePickLineResponse(x.ItemId, x.Qty, x.Location, x.OrderId, x.TaskId, x.Status))
                .OrderBy(x => x.Location, StringComparer.Ordinal)
                .ThenBy(x => x.ItemId)
                .ToArray());
    }

    public sealed record CreateWaveRequest(IReadOnlyList<Guid> OrderIds, string? AssignedOperator);
    public sealed record AssignWaveRequest(string AssignedOperator);
    public sealed record WavePickLineResponse(int ItemId, decimal Qty, string Location, Guid OrderId, Guid TaskId, string Status);
    public sealed record WaveResponse(
        Guid Id,
        string WaveNumber,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? AssignedAt,
        DateTimeOffset? CompletedAt,
        string? AssignedOperator,
        IReadOnlyCollection<Guid> OrderIds,
        int TotalLines,
        int CompletedLines,
        IReadOnlyCollection<WavePickLineResponse> PickList);
}

[ApiController]
[WarehouseOperationValidation]
[Route("api/warehouse/v1/cross-dock")]
public sealed class CrossDockController : ControllerBase
{
    private readonly IAdvancedWarehouseStore _store;

    public CrossDockController(IAdvancedWarehouseStore store)
    {
        _store = store;
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult Create([FromBody] CreateCrossDockRequest request)
    {
        var user = User.Identity?.Name ?? "system";
        var record = _store.CreateCrossDock(new CrossDockCreateRequest(
            request.InboundShipmentLineId,
            request.OutboundOrderId,
            request.ItemId,
            request.Qty,
            user));
        return Ok(record);
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult List()
    {
        return Ok(_store.GetCrossDocks());
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult UpdateStatus(Guid id, [FromBody] UpdateCrossDockStatusRequest request)
    {
        var record = _store.UpdateCrossDockStatus(id, request.Status);
        return record is null ? NotFound() : Ok(record);
    }

    public sealed record CreateCrossDockRequest(int InboundShipmentLineId, Guid OutboundOrderId, int ItemId, decimal Qty);
    public sealed record UpdateCrossDockStatusRequest(string Status);
}

[ApiController]
[WarehouseOperationValidation]
[Route("api/warehouse/v1/advanced/qc")]
public sealed class QcAdvancedController : ControllerBase
{
    private readonly IAdvancedWarehouseStore _store;
    private readonly IObjectStorageService _objects;

    public QcAdvancedController(IAdvancedWarehouseStore store, IObjectStorageService objects)
    {
        _store = store;
        _objects = objects;
    }

    [HttpPost("checklist-templates")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public IActionResult CreateTemplate([FromBody] CreateChecklistTemplateRequest request)
    {
        var user = User.Identity?.Name ?? "system";
        var template = _store.CreateChecklistTemplate(new QcChecklistTemplateCreateRequest(
            request.Name,
            request.CategoryCode,
            request.SupplierId,
            request.Items,
            user));
        return Ok(template);
    }

    [HttpGet("checklist-templates")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public IActionResult ListTemplates()
    {
        return Ok(_store.GetChecklistTemplates());
    }

    [HttpPost("defects")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public IActionResult CreateDefect([FromBody] CreateQcDefectRequest request)
    {
        var user = User.Identity?.Name ?? "system";
        var defect = _store.CreateQcDefect(new QcDefectCreateRequest(
            request.ItemId,
            request.LotNumber,
            request.SupplierId,
            request.DefectType,
            request.Severity,
            request.Notes,
            user));
        return Ok(defect);
    }

    [HttpGet("defects")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public IActionResult ListDefects()
    {
        return Ok(_store.GetQcDefects());
    }

    [HttpPost("defects/{defectId:guid}/attachments")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    [RequestSizeLimit(5_000_000)]
    public async Task<IActionResult> UploadAttachment(Guid defectId, IFormFile file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest(new { message = "Attachment file is required." });
        }

        if (!_store.GetQcDefects().Any(x => x.Id == defectId)) return NotFound();
        var attachmentId = Guid.NewGuid();
        var key = $"qc/{defectId:N}/{attachmentId:N}";
        await using (var stream = file.OpenReadStream())
            await _objects.PutObjectAsync(key, stream, file.Length, file.ContentType, cancellationToken);
        try
        {
            var attachment = new QcAttachmentRecord(attachmentId, Path.GetFileName(file.FileName), file.ContentType,
                $"/api/warehouse/v1/advanced/qc/defects/{defectId}/attachments/{attachmentId}", DateTimeOffset.UtcNow, User.Identity?.Name ?? "system");
            var defect = _store.AddQcAttachment(defectId, attachment);
            return defect is null ? NotFound() : Ok(defect);
        }
        catch
        {
            await _objects.DeleteObjectAsync(key, cancellationToken);
            throw;
        }
    }

    [HttpGet("defects/{defectId:guid}/attachments/{attachmentId:guid}")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> DownloadAttachment(Guid defectId, Guid attachmentId, CancellationToken ct)
    {
        var attachment = _store.GetQcDefects().SingleOrDefault(x => x.Id == defectId)?.Attachments.SingleOrDefault(x => x.Id == attachmentId);
        if (attachment is null) return NotFound();
        var stream = await _objects.GetObjectAsync($"qc/{defectId:N}/{attachmentId:N}", ct);
        return File(stream, attachment.ContentType, attachment.FileName);
    }

    public sealed record CreateChecklistTemplateRequest(
        string Name,
        string? CategoryCode,
        int? SupplierId,
        IReadOnlyList<QcChecklistTemplateItemCreateRequest> Items);

    public sealed record CreateQcDefectRequest(
        int ItemId,
        string? LotNumber,
        int? SupplierId,
        string DefectType,
        string Severity,
        string? Notes);
}

[ApiController]
[WarehouseOperationValidation]
[Route("api/warehouse/v1/rmas")]
public sealed class RmaController : ControllerBase
{
    private readonly IAdvancedWarehouseStore _store;

    public RmaController(IAdvancedWarehouseStore store)
    {
        _store = store;
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.SalesAdminOrManager)]
    public IActionResult Create([FromBody] CreateRmaRequest request)
    {
        if (request.Lines is null || request.Lines.Count == 0)
        {
            return BadRequest(new { message = "At least one RMA line is required." });
        }

        var rma = _store.CreateRma(new RmaCreateRequest(
            request.SalesOrderId,
            request.Reason,
            request.Lines,
            User.Identity?.Name ?? "system"));

        return Ok(ToResponse(rma));
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult List()
    {
        return Ok(_store.GetRmas().Select(ToResponse));
    }

    [HttpPost("{id:guid}/receive")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> Receive(Guid id, CancellationToken ct)
    {
        var rma = await _store.ReceiveRmaAsync(id, User.Identity?.Name ?? "system", ct);
        return rma is null ? NotFound() : Ok(ToResponse(rma));
    }

    [HttpPost("{id:guid}/inspect")]
    [Authorize(Policy = WarehousePolicies.QcOrManager)]
    public async Task<IActionResult> Inspect(Guid id, [FromBody] InspectRmaRequest request, CancellationToken ct)
    {
        var rma = await _store.InspectRmaAsync(id, request.Disposition, request.CreditAmount, User.Identity?.Name ?? "system", ct);
        return rma is null ? NotFound() : Ok(ToResponse(rma));
    }

    private static RmaResponse ToResponse(RmaRecord source)
    {
        return new RmaResponse(
            source.Id,
            source.RmaNumber,
            source.SalesOrderId,
            source.Reason,
            source.Status.ToString(),
            source.CreatedAt,
            source.ReceivedAt,
            source.InspectedAt,
            source.Disposition,
            source.CreditAmount,
            source.CreatedBy,
            source.UpdatedBy,
            source.Lines.Select(x => new RmaLineResponse(x.Id, x.ItemId, x.Qty, x.ReasonCode, x.SalesOrderLineId)).ToArray());
    }

    public sealed record CreateRmaRequest(Guid SalesOrderId, string Reason, IReadOnlyList<RmaLineCreateRequest> Lines);
    public sealed record InspectRmaRequest(string Disposition, decimal? CreditAmount);
    public sealed record RmaLineResponse(Guid Id, int ItemId, decimal Qty, string? ReasonCode, Guid? SalesOrderLineId);
    public sealed record RmaResponse(
        Guid Id,
        string RmaNumber,
        Guid SalesOrderId,
        string Reason,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? ReceivedAt,
        DateTimeOffset? InspectedAt,
        string? Disposition,
        decimal? CreditAmount,
        string CreatedBy,
        string? UpdatedBy,
        IReadOnlyCollection<RmaLineResponse> Lines);
}

[ApiController]
[WarehouseOperationValidation]
[Route("api/warehouse/v1/serials")]
public sealed class SerialsController : ControllerBase
{
    private readonly IAdvancedWarehouseStore _store;

    public SerialsController(IAdvancedWarehouseStore store)
    {
        _store = store;
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult Register([FromBody] RegisterSerialRequest request)
    {
        var serial = _store.RegisterSerial(new SerialRegisterRequest(request.ItemId, request.Value,
            request.Location, request.WarrantyExpiryDate, User.Identity?.Name ?? "system"));
        return Ok(ToResponse(serial));
    }

    [HttpPost("{id:guid}/status")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult Transition(Guid id, [FromBody] TransitionSerialRequest request)
    {
        var serial = _store.TransitionSerial(id, new SerialTransitionRequest(
            request.Status,
            request.Location,
            User.Identity?.Name ?? "system"));

        return serial is null ? NotFound() : Ok(ToResponse(serial));
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public IActionResult Search([FromQuery] string? serial, [FromQuery] int? itemId, [FromQuery] string? status)
    {
        return Ok(_store.SearchSerials(serial, itemId, status).Select(ToResponse));
    }

    private static object ToResponse(SerialRecord serial) => new
    {
        serial.Id, serial.ItemId, serial.Value, Status = serial.Status.ToString(), serial.Location, serial.WarrantyExpiryDate,
        History = serial.History.Select(x => new { x.ChangedAt, PreviousStatus = x.PreviousStatus?.ToString(),
            NewStatus = x.NewStatus.ToString(), x.Location, x.UpdatedBy })
    };

    public sealed record RegisterSerialRequest(int ItemId, string Value, string? Location, DateOnly? WarrantyExpiryDate);
    public sealed record TransitionSerialRequest(string Status, string? Location);
}

[ApiController]
[WarehouseOperationValidation]
[Route("api/warehouse/v1/analytics")]
public sealed class AdvancedAnalyticsController : ControllerBase
{
    private readonly IAdvancedWarehouseStore _store;
    private readonly WarehouseDbContext _dbContext;

    public AdvancedAnalyticsController(IAdvancedWarehouseStore store, WarehouseDbContext dbContext)
    {
        _store = store;
        _dbContext = dbContext;
    }

    [HttpGet("fulfillment-kpis")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> FulfillmentKpis(CancellationToken cancellationToken)
    {
        var salesOrders = await _dbContext.SalesOrders
            .Select(x => new { x.OrderDate, x.ShippedAt, x.RequestedDeliveryDate })
            .ToListAsync(cancellationToken);

        var totalOrders = salesOrders.Count;
        var shippedOrders = salesOrders.Count(x => x.ShippedAt != null);
        var onTimeOrders = salesOrders.Count(x =>
            x.ShippedAt != null &&
            x.RequestedDeliveryDate != null &&
            x.ShippedAt <= new DateTimeOffset(x.RequestedDeliveryDate.Value.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero));

        var pickDurations = await _dbContext.OutboundOrders
            .Where(x => x.PickedAt != null)
            .Select(x => new { x.OrderDate, PickedAt = x.PickedAt!.Value })
            .ToListAsync(cancellationToken);

        var avgPickMinutes = pickDurations.Count == 0
            ? 0
            : pickDurations.Average(x => (x.PickedAt - x.OrderDate).TotalMinutes);

        var trend = salesOrders
            .Where(x => x.OrderDate >= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)))
            .GroupBy(x => x.OrderDate)
            .Select(g => new
            {
                Date = g.Key,
                Orders = g.Count(),
                Shipped = g.Count(x => x.ShippedAt != null)
            })
            .OrderBy(x => x.Date)
            .ToList();

        var onTimePercent = shippedOrders == 0 ? 0m : decimal.Round((decimal)onTimeOrders / shippedOrders * 100m, 2);

        return Ok(new
        {
            totalOrders,
            shippedOrders,
            onTimePercent,
            averagePickMinutes = Math.Round(avgPickMinutes, 2),
            trend
        });
    }

    [HttpGet("qc-late-shipments")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> QcAndLateShipments(CancellationToken cancellationToken)
    {
        var defects = _store.GetQcDefects();
        var defectsByType = defects
            .GroupBy(x => x.DefectType)
            .Select(g => new { defectType = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        var defectsBySupplier = defects
            .Where(x => x.SupplierId.HasValue)
            .GroupBy(x => x.SupplierId!.Value)
            .Select(g => new { supplierId = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ToList();

        var lateShipments = await _dbContext.OutboundOrders
            .Where(x => x.RequestedShipDate != null && x.ShippedAt != null && x.ShippedAt > x.RequestedShipDate)
            .Select(x => new
            {
                x.Id,
                x.OrderNumber,
                x.RequestedShipDate,
                x.ShippedAt,
                currentStatus = x.Status.ToString()
            })
            .OrderByDescending(x => x.ShippedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        return Ok(new
        {
            defectCount = _store.GetQcDefectCount(),
            defectsByType,
            defectsBySupplier,
            lateShipmentsCount = lateShipments.Count,
            lateShipments
        });
    }
}
