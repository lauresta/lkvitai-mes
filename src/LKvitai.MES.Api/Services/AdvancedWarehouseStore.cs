using LKvitai.MES.Api.Security;

namespace LKvitai.MES.Api.Services;

public interface IAdvancedWarehouseStore
{
    WaveRecord CreateWave(IReadOnlyCollection<Guid> orderIds, string? assignedOperator);
    IReadOnlyList<WaveRecord> GetWaves(string? status);
    WaveRecord? GetWave(Guid id);
    WaveRecord? AssignWave(Guid id, string assignedOperator);
    WaveRecord? StartWave(Guid id);
    WaveRecord? CompleteWaveLines(Guid id, int lines);

    CrossDockRecord CreateCrossDock(CrossDockCreateRequest request);
    IReadOnlyList<CrossDockRecord> GetCrossDocks();
    CrossDockRecord? UpdateCrossDockStatus(Guid id, string status);

    QcChecklistTemplateRecord CreateChecklistTemplate(QcChecklistTemplateCreateRequest request);
    IReadOnlyList<QcChecklistTemplateRecord> GetChecklistTemplates();
    QcDefectRecord CreateQcDefect(QcDefectCreateRequest request);
    IReadOnlyList<QcDefectRecord> GetQcDefects();
    QcDefectRecord? AddQcAttachment(Guid defectId, QcAttachmentRecord attachment);

    RmaRecord CreateRma(RmaCreateRequest request);
    IReadOnlyList<RmaRecord> GetRmas();
    RmaRecord? ReceiveRma(Guid id, string receivedBy);
    RmaRecord? InspectRma(Guid id, string disposition, decimal? creditAmount, string inspectedBy);

    HuRecord EnsureHu(Guid huId, Guid? parentHuId);
    IReadOnlyList<HuRecord> SplitHu(Guid parentHuId, int childCount);
    HuRecord? MergeHu(Guid parentHuId, IReadOnlyCollection<Guid> childIds);
    IReadOnlyList<HuRecord> GetHuHierarchy(Guid huId);

    SerialRecord RegisterSerial(SerialRegisterRequest request);
    SerialRecord? TransitionSerial(Guid id, SerialTransitionRequest request);
    IReadOnlyList<SerialRecord> SearchSerials(string? serial, int? itemId, string? status);

    int GetQcDefectCount();
}

public sealed class AdvancedWarehouseStore : IAdvancedWarehouseStore
{
    private readonly object _sync = new();

    private readonly Dictionary<Guid, WaveRecord> _waves = new();
    private readonly Dictionary<Guid, CrossDockRecord> _crossDocks = new();
    private readonly Dictionary<Guid, QcChecklistTemplateRecord> _templates = new();
    private readonly Dictionary<Guid, QcDefectRecord> _defects = new();
    private readonly Dictionary<Guid, RmaRecord> _rmas = new();
    private readonly Dictionary<Guid, HuRecord> _hus = new();
    private readonly Dictionary<Guid, SerialRecord> _serials = new();

    private int _waveSequence;
    private int _crossDockSequence;
    private int _rmaSequence;

    public WaveRecord CreateWave(IReadOnlyCollection<Guid> orderIds, string? assignedOperator)
    {
        if (orderIds.Count == 0)
        {
            throw new ArgumentException("At least one order ID is required.", nameof(orderIds));
        }

        lock (_sync)
        {
            foreach (var existing in _waves.Values)
            {
                if (existing.OrderIds.Count == orderIds.Count &&
                    !existing.OrderIds.Except(orderIds).Any())
                {
                    if (!string.IsNullOrWhiteSpace(assignedOperator) &&
                        existing.Status is WaveStatus.Created &&
                        string.IsNullOrWhiteSpace(existing.AssignedOperator))
                    {
                        existing.AssignedOperator = assignedOperator;
                        existing.AssignedAt = DateTimeOffset.UtcNow;
                        existing.Status = WaveStatus.Assigned;
                    }

                    return existing;
                }
            }

            _waveSequence++;
            var now = DateTimeOffset.UtcNow;
            var pickList = BuildPickList(orderIds);
            var record = new WaveRecord
            {
                Id = Guid.NewGuid(),
                WaveNumber = $"WAVE-{_waveSequence:0000}",
                Status = string.IsNullOrWhiteSpace(assignedOperator) ? WaveStatus.Created : WaveStatus.Assigned,
                CreatedAt = now,
                AssignedAt = string.IsNullOrWhiteSpace(assignedOperator) ? null : now,
                AssignedOperator = assignedOperator,
                OrderIds = orderIds.Distinct().ToList(),
                PickList = pickList,
                CompletedLines = 0
            };

            _waves[record.Id] = record;
            return record;
        }
    }

    public IReadOnlyList<WaveRecord> GetWaves(string? status)
    {
        lock (_sync)
        {
            return _waves.Values
                .Where(x => string.IsNullOrWhiteSpace(status) || x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.CreatedAt)
                .Select(Clone)
                .ToList();
        }
    }

    public WaveRecord? GetWave(Guid id)
    {
        lock (_sync)
        {
            return _waves.TryGetValue(id, out var wave) ? Clone(wave) : null;
        }
    }

    public WaveRecord? AssignWave(Guid id, string assignedOperator)
    {
        lock (_sync)
        {
            if (!_waves.TryGetValue(id, out var wave))
            {
                return null;
            }

            wave.AssignedOperator = assignedOperator;
            wave.AssignedAt = DateTimeOffset.UtcNow;
            wave.Status = WaveStatus.Assigned;
            return Clone(wave);
        }
    }

    public WaveRecord? StartWave(Guid id)
    {
        lock (_sync)
        {
            if (!_waves.TryGetValue(id, out var wave))
            {
                return null;
            }

            wave.Status = WaveStatus.Picking;
            return Clone(wave);
        }
    }

    public WaveRecord? CompleteWaveLines(Guid id, int lines)
    {
        lock (_sync)
        {
            if (!_waves.TryGetValue(id, out var wave))
            {
                return null;
            }

            wave.CompletedLines = Math.Min(wave.TotalLines, wave.CompletedLines + Math.Max(0, lines));
            if (wave.CompletedLines >= wave.TotalLines)
            {
                wave.Status = WaveStatus.Completed;
                wave.CompletedAt = DateTimeOffset.UtcNow;
            }

            return Clone(wave);
        }
    }

    public CrossDockRecord CreateCrossDock(CrossDockCreateRequest request)
    {
        lock (_sync)
        {
            _crossDockSequence++;
            var now = DateTimeOffset.UtcNow;
            var record = new CrossDockRecord(
                Guid.NewGuid(),
                $"XD-{_crossDockSequence:0000}",
                request.InboundShipmentId,
                request.OutboundOrderId,
                request.ItemId,
                request.Qty,
                "PENDING",
                now,
                null,
                request.CreatedBy);

            _crossDocks[record.Id] = record;
            return record;
        }
    }

    public IReadOnlyList<CrossDockRecord> GetCrossDocks()
    {
        lock (_sync)
        {
            return _crossDocks.Values.OrderByDescending(x => x.CreatedAt).ToList();
        }
    }

    public CrossDockRecord? UpdateCrossDockStatus(Guid id, string status)
    {
        lock (_sync)
        {
            if (!_crossDocks.TryGetValue(id, out var record))
            {
                return null;
            }

            var updated = record with
            {
                Status = status.ToUpperInvariant(),
                CompletedAt = status.Equals("COMPLETED", StringComparison.OrdinalIgnoreCase)
                    ? DateTimeOffset.UtcNow
                    : null
            };

            _crossDocks[id] = updated;
            return updated;
        }
    }

    public QcChecklistTemplateRecord CreateChecklistTemplate(QcChecklistTemplateCreateRequest request)
    {
        lock (_sync)
        {
            var template = new QcChecklistTemplateRecord(
                Guid.NewGuid(),
                request.Name,
                request.CategoryCode,
                request.SupplierId,
                request.Items.Select((x, idx) => new QcChecklistItemRecord(Guid.NewGuid(), idx + 1, x.Step, x.Required)).ToList(),
                DateTimeOffset.UtcNow,
                request.CreatedBy);

            _templates[template.Id] = template;
            return template;
        }
    }

    public IReadOnlyList<QcChecklistTemplateRecord> GetChecklistTemplates()
    {
        lock (_sync)
        {
            return _templates.Values.OrderBy(x => x.Name).ToList();
        }
    }

    public QcDefectRecord CreateQcDefect(QcDefectCreateRequest request)
    {
        lock (_sync)
        {
            var defect = new QcDefectRecord(
                Guid.NewGuid(),
                request.ItemId,
                request.LotNumber,
                request.SupplierId,
                request.DefectType.ToUpperInvariant(),
                request.Severity.ToUpperInvariant(),
                request.Notes,
                DateTimeOffset.UtcNow,
                request.CreatedBy,
                new List<QcAttachmentRecord>());

            _defects[defect.Id] = defect;
            return defect;
        }
    }

    public IReadOnlyList<QcDefectRecord> GetQcDefects()
    {
        lock (_sync)
        {
            return _defects.Values.OrderByDescending(x => x.CreatedAt).ToList();
        }
    }

    public QcDefectRecord? AddQcAttachment(Guid defectId, QcAttachmentRecord attachment)
    {
        lock (_sync)
        {
            if (!_defects.TryGetValue(defectId, out var defect))
            {
                return null;
            }

            var attachments = defect.Attachments.ToList();
            attachments.Add(attachment);
            var updated = defect with { Attachments = attachments };
            _defects[defectId] = updated;
            return updated;
        }
    }

    public RmaRecord CreateRma(RmaCreateRequest request)
    {
        lock (_sync)
        {
            _rmaSequence++;
            var record = new RmaRecord
            {
                Id = Guid.NewGuid(),
                RmaNumber = $"RMA-{_rmaSequence:0000}",
                SalesOrderId = request.SalesOrderId,
                Reason = request.Reason,
                Status = RmaStatus.PendingReceipt,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = request.CreatedBy,
                Lines = request.Lines.Select(x => new RmaLineRecord(Guid.NewGuid(), x.ItemId, x.Qty, x.ReasonCode)).ToList()
            };

            _rmas[record.Id] = record;
            return Clone(record);
        }
    }

    public IReadOnlyList<RmaRecord> GetRmas()
    {
        lock (_sync)
        {
            return _rmas.Values.OrderByDescending(x => x.CreatedAt).Select(Clone).ToList();
        }
    }

    public RmaRecord? ReceiveRma(Guid id, string receivedBy)
    {
        lock (_sync)
        {
            if (!_rmas.TryGetValue(id, out var rma))
            {
                return null;
            }

            rma.Status = RmaStatus.Received;
            rma.ReceivedAt = DateTimeOffset.UtcNow;
            rma.UpdatedBy = receivedBy;
            return Clone(rma);
        }
    }

    public RmaRecord? InspectRma(Guid id, string disposition, decimal? creditAmount, string inspectedBy)
    {
        lock (_sync)
        {
            if (!_rmas.TryGetValue(id, out var rma))
            {
                return null;
            }

            rma.Status = RmaStatus.Inspected;
            rma.InspectedAt = DateTimeOffset.UtcNow;
            rma.Disposition = disposition;
            rma.CreditAmount = creditAmount;
            rma.UpdatedBy = inspectedBy;
            if (disposition.Equals("RESTOCK", StringComparison.OrdinalIgnoreCase))
            {
                rma.Status = RmaStatus.Restocked;
            }
            else if (disposition.Equals("SCRAP", StringComparison.OrdinalIgnoreCase))
            {
                rma.Status = RmaStatus.Scrapped;
            }

            return Clone(rma);
        }
    }

    public HuRecord EnsureHu(Guid huId, Guid? parentHuId)
    {
        lock (_sync)
        {
            if (_hus.TryGetValue(huId, out var existing))
            {
                return existing;
            }

            var hu = new HuRecord(huId, parentHuId, new List<Guid>(), DateTimeOffset.UtcNow);
            _hus[huId] = hu;
            if (parentHuId.HasValue)
            {
                EnsureHu(parentHuId.Value, null);
                LinkChild(parentHuId.Value, huId);
            }

            return hu;
        }
    }

    public IReadOnlyList<HuRecord> SplitHu(Guid parentHuId, int childCount)
    {
        if (childCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(childCount));
        }

        lock (_sync)
        {
            EnsureHu(parentHuId, null);
            var children = new List<HuRecord>();
            for (var i = 0; i < childCount; i++)
            {
                var childId = Guid.NewGuid();
                var child = new HuRecord(childId, parentHuId, new List<Guid>(), DateTimeOffset.UtcNow);
                _hus[childId] = child;
                LinkChild(parentHuId, childId);
                children.Add(child);
            }

            return children;
        }
    }

    public HuRecord? MergeHu(Guid parentHuId, IReadOnlyCollection<Guid> childIds)
    {
        lock (_sync)
        {
            if (!_hus.TryGetValue(parentHuId, out var parent))
            {
                return null;
            }

            foreach (var childId in childIds)
            {
                if (!_hus.Remove(childId))
                {
                    continue;
                }

                parent.Children.Remove(childId);
            }

            return parent;
        }
    }

    public IReadOnlyList<HuRecord> GetHuHierarchy(Guid huId)
    {
        lock (_sync)
        {
            if (!_hus.TryGetValue(huId, out var root))
            {
                return Array.Empty<HuRecord>();
            }

            var result = new List<HuRecord>();
            Traverse(root, result);
            return result;
        }
    }

    public SerialRecord RegisterSerial(SerialRegisterRequest request)
    {
        lock (_sync)
        {
            if (_serials.Values.Any(x => x.Value.Equals(request.Value, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException("Serial already exists.");
            }

            var record = new SerialRecord
            {
                Id = Guid.NewGuid(),
                ItemId = request.ItemId,
                Value = request.Value,
                Status = SerialStatus.Received,
                Location = request.Location,
                WarrantyExpiryDate = request.WarrantyExpiryDate,
                History =
                [
                    new SerialHistoryRecord(DateTimeOffset.UtcNow, null, SerialStatus.Received, request.Location, request.UpdatedBy)
                ]
            };

            _serials[record.Id] = record;
            return Clone(record);
        }
    }

    public SerialRecord? TransitionSerial(Guid id, SerialTransitionRequest request)
    {
        lock (_sync)
        {
            if (!_serials.TryGetValue(id, out var record))
            {
                return null;
            }

            var next = Enum.Parse<SerialStatus>(request.Status, true);
            record.History.Add(new SerialHistoryRecord(
                DateTimeOffset.UtcNow,
                record.Status,
                next,
                request.Location,
                request.UpdatedBy));
            record.Status = next;
            record.Location = request.Location;
            return Clone(record);
        }
    }

    public IReadOnlyList<SerialRecord> SearchSerials(string? serial, int? itemId, string? status)
    {
        lock (_sync)
        {
            return _serials.Values
                .Where(x => string.IsNullOrWhiteSpace(serial) || x.Value.Contains(serial, StringComparison.OrdinalIgnoreCase))
                .Where(x => !itemId.HasValue || x.ItemId == itemId.Value)
                .Where(x => string.IsNullOrWhiteSpace(status) || x.Status.ToString().Equals(status, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.Value)
                .Select(Clone)
                .ToList();
        }
    }

    public int GetQcDefectCount()
    {
        lock (_sync)
        {
            return _defects.Count;
        }
    }

    private static List<WavePickLineRecord> BuildPickList(IReadOnlyCollection<Guid> orderIds)
    {
        var lines = new List<WavePickLineRecord>();
        var index = 0;
        foreach (var orderId in orderIds.Distinct())
        {
            var seed = Math.Abs(orderId.GetHashCode());
            var aisle = (seed % 8) + 1;
            var rack = ((seed / 8) % 20) + 1;
            var level = ((seed / 160) % 5) + 1;
            var bin = ((seed / 800) % 40) + 1;
            lines.Add(new WavePickLineRecord(
                (seed % 500) + 1,
                (seed % 7) + 1,
                $"A{aisle:D2}-R{rack:D2}-L{level:D2}-B{bin:D2}",
                orderId,
                index++));
        }

        return lines
            .OrderBy(x => x.Location, StringComparer.Ordinal)
            .ThenBy(x => x.SortOrder)
            .ToList();
    }

    private void Traverse(HuRecord node, ICollection<HuRecord> buffer)
    {
        buffer.Add(node);
        foreach (var childId in node.Children)
        {
            if (_hus.TryGetValue(childId, out var child))
            {
                Traverse(child, buffer);
            }
        }
    }

    private void LinkChild(Guid parentId, Guid childId)
    {
        if (!_hus.TryGetValue(parentId, out var parent))
        {
            return;
        }

        if (CreatesCycle(parentId, childId))
        {
            throw new InvalidOperationException("Circular HU hierarchy is not allowed.");
        }

        if (!parent.Children.Contains(childId))
        {
            parent.Children.Add(childId);
        }
    }

    private bool CreatesCycle(Guid parentId, Guid childId)
    {
        if (parentId == childId)
        {
            return true;
        }

        var visited = new HashSet<Guid>();
        var current = parentId;
        while (_hus.TryGetValue(current, out var currentHu) && currentHu.ParentHuId.HasValue)
        {
            if (!visited.Add(current))
            {
                break;
            }

            current = currentHu.ParentHuId.Value;
            if (current == childId)
            {
                return true;
            }
        }

        return false;
    }

    private static WaveRecord Clone(WaveRecord source)
    {
        return new WaveRecord
        {
            Id = source.Id,
            WaveNumber = source.WaveNumber,
            Status = source.Status,
            CreatedAt = source.CreatedAt,
            AssignedAt = source.AssignedAt,
            CompletedAt = source.CompletedAt,
            AssignedOperator = source.AssignedOperator,
            OrderIds = source.OrderIds.ToList(),
            PickList = source.PickList.ToList(),
            CompletedLines = source.CompletedLines
        };
    }

    private static RmaRecord Clone(RmaRecord source)
    {
        return new RmaRecord
        {
            Id = source.Id,
            RmaNumber = source.RmaNumber,
            SalesOrderId = source.SalesOrderId,
            Reason = source.Reason,
            Status = source.Status,
            CreatedAt = source.CreatedAt,
            ReceivedAt = source.ReceivedAt,
            InspectedAt = source.InspectedAt,
            Disposition = source.Disposition,
            CreditAmount = source.CreditAmount,
            CreatedBy = source.CreatedBy,
            UpdatedBy = source.UpdatedBy,
            Lines = source.Lines.ToList()
        };
    }

    private static SerialRecord Clone(SerialRecord source)
    {
        return new SerialRecord
        {
            Id = source.Id,
            ItemId = source.ItemId,
            Value = source.Value,
            Status = source.Status,
            Location = source.Location,
            WarrantyExpiryDate = source.WarrantyExpiryDate,
            History = source.History.ToList()
        };
    }
}

public enum WaveStatus
{
    Created,
    Assigned,
    Picking,
    Completed,
    Cancelled
}

public sealed class WaveRecord
{
    public Guid Id { get; set; }
    public string WaveNumber { get; set; } = string.Empty;
    public WaveStatus Status { get; set; } = WaveStatus.Created;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? AssignedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? AssignedOperator { get; set; }
    public List<Guid> OrderIds { get; set; } = new();
    public List<WavePickLineRecord> PickList { get; set; } = new();
    public int TotalLines => PickList.Count;
    public int CompletedLines { get; set; }
}

public sealed record WavePickLineRecord(int ItemId, decimal Qty, string Location, Guid OrderId, int SortOrder);

public sealed record CrossDockCreateRequest(Guid InboundShipmentId, Guid OutboundOrderId, int ItemId, decimal Qty, string CreatedBy);
public sealed record CrossDockRecord(
    Guid Id,
    string CrossDockNumber,
    Guid InboundShipmentId,
    Guid OutboundOrderId,
    int ItemId,
    decimal Qty,
    string Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt,
    string CreatedBy);

public sealed record QcChecklistTemplateCreateRequest(
    string Name,
    string? CategoryCode,
    int? SupplierId,
    IReadOnlyList<QcChecklistTemplateItemCreateRequest> Items,
    string CreatedBy);

public sealed record QcChecklistTemplateItemCreateRequest(string Step, bool Required);
public sealed record QcChecklistTemplateRecord(
    Guid Id,
    string Name,
    string? CategoryCode,
    int? SupplierId,
    IReadOnlyList<QcChecklistItemRecord> Items,
    DateTimeOffset CreatedAt,
    string CreatedBy);

public sealed record QcChecklistItemRecord(Guid Id, int Sequence, string Step, bool Required);

public sealed record QcDefectCreateRequest(
    int ItemId,
    string? LotNumber,
    int? SupplierId,
    string DefectType,
    string Severity,
    string? Notes,
    string CreatedBy);

public sealed record QcDefectRecord(
    Guid Id,
    int ItemId,
    string? LotNumber,
    int? SupplierId,
    string DefectType,
    string Severity,
    string? Notes,
    DateTimeOffset CreatedAt,
    string CreatedBy,
    IReadOnlyList<QcAttachmentRecord> Attachments);

public sealed record QcAttachmentRecord(Guid Id, string FileName, string ContentType, string Url, DateTimeOffset UploadedAt, string UploadedBy);

public sealed record RmaCreateRequest(Guid SalesOrderId, string Reason, IReadOnlyList<RmaLineCreateRequest> Lines, string CreatedBy);
public sealed record RmaLineCreateRequest(int ItemId, decimal Qty, string? ReasonCode);

public enum RmaStatus
{
    PendingReceipt,
    Received,
    Inspected,
    Restocked,
    Scrapped
}

public sealed class RmaRecord
{
    public Guid Id { get; set; }
    public string RmaNumber { get; set; } = string.Empty;
    public Guid SalesOrderId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public RmaStatus Status { get; set; } = RmaStatus.PendingReceipt;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReceivedAt { get; set; }
    public DateTimeOffset? InspectedAt { get; set; }
    public string? Disposition { get; set; }
    public decimal? CreditAmount { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string? UpdatedBy { get; set; }
    public List<RmaLineRecord> Lines { get; set; } = new();
}

public sealed record RmaLineRecord(Guid Id, int ItemId, decimal Qty, string? ReasonCode);

public sealed class HuRecord
{
    public HuRecord(Guid huId, Guid? parentHuId, List<Guid> children, DateTimeOffset createdAt)
    {
        HuId = huId;
        ParentHuId = parentHuId;
        Children = children;
        CreatedAt = createdAt;
    }

    public Guid HuId { get; }
    public Guid? ParentHuId { get; set; }
    public List<Guid> Children { get; }
    public DateTimeOffset CreatedAt { get; }
}

public sealed record SerialRegisterRequest(int ItemId, string Value, string? Location, DateOnly? WarrantyExpiryDate, string UpdatedBy);
public sealed record SerialTransitionRequest(string Status, string? Location, string UpdatedBy);

public enum SerialStatus
{
    Received,
    Available,
    Issued,
    Returned,
    Scrapped
}

public sealed class SerialRecord
{
    public Guid Id { get; set; }
    public int ItemId { get; set; }
    public string Value { get; set; } = string.Empty;
    public SerialStatus Status { get; set; }
    public string? Location { get; set; }
    public DateOnly? WarrantyExpiryDate { get; set; }
    public List<SerialHistoryRecord> History { get; set; } = new();
}

public sealed record SerialHistoryRecord(
    DateTimeOffset ChangedAt,
    SerialStatus? PreviousStatus,
    SerialStatus NewStatus,
    string? Location,
    string UpdatedBy);
