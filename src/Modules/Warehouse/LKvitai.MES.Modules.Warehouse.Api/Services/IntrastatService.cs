using System.Security.Cryptography;
using System.Text;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public sealed record IntrastatXmlOutcome(IntrastatXmlExport? Export, IntrastatXmlResult Result, bool IsNewVersion);

/// <summary>Intrastat arrivals: report data of a month, declarant settings and the stored XML versions.</summary>
public sealed class IntrastatService
{
    private readonly WarehouseDbContext _db;

    public IntrastatService(WarehouseDbContext db) => _db = db;

    public async Task<(IReadOnlyList<IntrastatRow> Rows, IReadOnlyList<string> Warnings)> LoadAsync(int year, int month, CancellationToken ct)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1);
        var shipments = await _db.InboundShipments.AsNoTracking()
            .Include(s => s.Supplier)
            .Include(s => s.Lines).ThenInclude(l => l.Item)
            .Where(s => s.InvoiceDate >= from && s.InvoiceDate < to && s.DocumentKind == "Invoice" && s.PostingStatus == "Posted")
            .ToListAsync(ct);

        var credits = await _db.SupplierCreditDocuments.AsNoTracking()
            .Include(c => c.Lines).ThenInclude(l => l.Item)
            .Where(c => c.CreditDate >= from && c.CreditDate < to && c.PostingStatus == "Posted")
            .ToListAsync(ct);
        var originalIds = credits.Select(c => c.OriginalShipmentId).Distinct().ToList();
        var originals = originalIds.Count == 0
            ? new Dictionary<int, InboundShipment>()
            : await _db.InboundShipments.AsNoTracking()
                .Include(s => s.Supplier)
                .Where(s => originalIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, ct);

        var collected = IntrastatReport.Collect(shipments, credits, originals);
        return (IntrastatReport.Build(collected.Lines), collected.Warnings);
    }

    public async Task<IntrastatDeclarant> GetDeclarantAsync(CancellationToken ct)
        => await _db.IntrastatDeclarants.AsNoTracking().FirstOrDefaultAsync(ct) ?? new IntrastatDeclarant();

    public async Task<IntrastatDeclarant> SaveDeclarantAsync(IntrastatDeclarant input, string user, CancellationToken ct)
    {
        var row = await _db.IntrastatDeclarants.FirstOrDefaultAsync(ct);
        if (row is null)
        {
            row = new IntrastatDeclarant();
            _db.IntrastatDeclarants.Add(row);
        }

        row.VatCode = Clean(input.VatCode)?.ToUpperInvariant();
        row.Title = Clean(input.Title);
        row.Street = Clean(input.Street);
        row.ZipCode = Clean(input.ZipCode);
        row.ContactPerson = Clean(input.ContactPerson);
        row.Email = Clean(input.Email);
        row.Phone = Clean(input.Phone);
        row.UpdatedAt = DateTimeOffset.UtcNow;
        row.UpdatedBy = user;
        await _db.SaveChangesAsync(ct);
        return row;
    }

    /// <summary>
    /// Builds the XML of the month. A file that is byte-identical to the latest version is not stored again; a changed
    /// file becomes the next version (a correction).
    /// </summary>
    public async Task<IntrastatXmlOutcome> GenerateAsync(int year, int month, string user, CancellationToken ct)
    {
        var (rows, warnings) = await LoadAsync(year, month, ct);
        var declarant = await GetDeclarantAsync(ct);
        var result = IntrastatXmlWriter.Write(declarant, year, month, rows, warnings);
        if (result.Xml is null) return new IntrastatXmlOutcome(null, result, false);

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(result.Xml))).ToLowerInvariant();
        var latest = await _db.IntrastatXmlExports
            .Where(e => e.Year == year && e.Month == month)
            .OrderByDescending(e => e.Version)
            .FirstOrDefaultAsync(ct);
        if (latest is not null && latest.Sha256 == hash) return new IntrastatXmlOutcome(latest, result, false);

        var export = new IntrastatXmlExport
        {
            Year = year, Month = month, Version = (latest?.Version ?? 0) + 1, GeneratedAt = DateTimeOffset.UtcNow,
            GeneratedBy = user, IsZeroReport = result.IsZeroReport, RowCount = result.ItemCount,
            TotalValueEur = result.TotalValueEur, TotalNetMassKg = result.TotalNetMassKg, Sha256 = hash, Xml = result.Xml
        };
        _db.IntrastatXmlExports.Add(export);
        await _db.SaveChangesAsync(ct);
        return new IntrastatXmlOutcome(export, result, true);
    }

    public async Task<IntrastatXmlExport?> MarkFiledAsync(int id, string user, CancellationToken ct)
    {
        var export = await _db.IntrastatXmlExports.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (export is null) return null;
        export.FiledAt ??= DateTimeOffset.UtcNow;
        export.FiledBy ??= user;
        await _db.SaveChangesAsync(ct);
        return export;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
