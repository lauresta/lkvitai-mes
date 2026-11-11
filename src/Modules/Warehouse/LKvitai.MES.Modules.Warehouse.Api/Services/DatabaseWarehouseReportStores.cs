using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed class DatabaseAgnumReconciliationReportStore(WarehouseDbContext db) : IAgnumReconciliationReportStore
{
    public void Save(AgnumReconciliationReport report)
    {
        var row = db.Set<AgnumReconciliationReportEntry>().Find(report.ReportId);
        if (row is null) { row = new() { Id = report.ReportId }; db.Add(row); }
        row.GeneratedAt = report.GeneratedAt; row.PayloadJson = JsonSerializer.Serialize(report); db.SaveChanges();
    }
    public bool TryGet(Guid reportId, out AgnumReconciliationReport? report)
    {
        var row = db.Set<AgnumReconciliationReportEntry>().AsNoTracking().SingleOrDefault(x => x.Id == reportId);
        report = row is null ? null : JsonSerializer.Deserialize<AgnumReconciliationReport>(row.PayloadJson);
        return report is not null;
    }
}

public sealed class DatabaseLabelPrintQueueStore(WarehouseDbContext db) : ILabelPrintQueueStore
{
    public PrintQueueItem Enqueue(string templateType, IReadOnlyDictionary<string, string> data, string? errorMessage = null)
    {
        var row = new LabelPrintJob { TemplateType = templateType, DataJson = JsonSerializer.Serialize(data), ErrorMessage = errorMessage ?? "" };
        db.Add(row); db.SaveChanges(); return Map(row);
    }
    public IReadOnlyList<PrintQueueItem> List(Func<PrintQueueItem, bool> predicate) => db.Set<LabelPrintJob>().AsNoTracking()
        .OrderBy(x => x.CreatedAt).AsEnumerable().Select(Map).Where(predicate).ToList();
    public PrintQueueItem? Get(Guid id)
    {
        var row = db.Set<LabelPrintJob>().AsNoTracking().SingleOrDefault(x => x.Id == id);
        return row is null ? null : Map(row);
    }
    public void Save(PrintQueueItem item)
    {
        var row = db.Set<LabelPrintJob>().Find(item.Id) ?? throw new KeyNotFoundException("Print job not found.");
        row.Status = item.Status.ToString(); row.RetryCount = item.RetryCount; row.LastAttemptAt = item.LastAttemptAt;
        row.ErrorMessage = item.ErrorMessage; db.SaveChanges();
    }
    private static PrintQueueItem Map(LabelPrintJob row) => new() { Id = row.Id, TemplateType = row.TemplateType,
        DataJson = row.DataJson, Status = Enum.Parse<PrintQueueStatus>(row.Status), RetryCount = row.RetryCount,
        CreatedAt = row.CreatedAt, LastAttemptAt = row.LastAttemptAt, ErrorMessage = row.ErrorMessage };
}
