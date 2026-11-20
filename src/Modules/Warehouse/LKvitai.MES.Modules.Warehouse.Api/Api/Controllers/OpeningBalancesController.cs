using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Api.Services.OpeningBalances;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using IDocumentStore = Marten.IDocumentStore;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

public sealed class OpeningBalanceLine
{
    public string Sku { get; set; } = string.Empty;
    public int ItemId { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public decimal ValueEur { get; set; }
    public int? StockAccountProfileId { get; set; }
    public string StockAccount { get; set; } = string.Empty;
}

public sealed class OpeningBalanceAccountTotal
{
    public int? StockAccountProfileId { get; set; }
    public string StockAccount { get; set; } = string.Empty;
    public string ProfileName { get; set; } = string.Empty;
    public int Rows { get; set; }
    public decimal Qty { get; set; }
    public decimal ValueEur { get; set; }
}

/// <summary>Marten document of one loaded opening balance file. Id is the SHA-256 of the file (idempotency key).</summary>
public sealed class OpeningBalanceDocument
{
    public string Id { get; set; } = string.Empty;
    public string Number { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime BusinessDate { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public List<OpeningBalanceLine> Lines { get; set; } = [];
    public List<OpeningBalanceAccountTotal> Totals { get; set; } = [];
    public decimal TotalQty { get; set; }
    public decimal TotalValueEur { get; set; }
}

public sealed class OpeningBalancePreviewRow
{
    public int RowNumber { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public decimal? Qty { get; set; }
    public decimal? ValueEur { get; set; }
    public string? ItemName { get; set; }
    public string? StockAccount { get; set; }
    public List<string> Errors { get; set; } = [];
}

public sealed class OpeningBalancePreviewResult
{
    public string? FileError { get; set; }
    public string FileHash { get; set; } = string.Empty;
    public DateTime BusinessDate { get; set; }
    public List<OpeningBalancePreviewRow> Rows { get; set; } = [];
    public List<OpeningBalanceAccountTotal> Totals { get; set; } = [];
    public int ErrorRows { get; set; }
    public bool AlreadyLoaded { get; set; }
    public string? ExistingNumber { get; set; }
}

public sealed class OpeningBalanceConfirmResult
{
    public bool AlreadyLoaded { get; set; }
    public string Number { get; set; } = string.Empty;
    public int Lines { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalValueEur { get; set; }
    public List<OpeningBalanceAccountTotal> Totals { get; set; } = [];
}

/// <summary>Opening balances (blueprint 4.10): CSV preview and idempotent load, never sent to Agnum.</summary>
[ApiController]
[Authorize(Policy = WarehousePolicies.InventoryAccountantOrManager)]
[Route("api/warehouse/v1/opening-balances")]
public sealed class OpeningBalancesController : ControllerBase
{
    public static readonly DateTime OpeningDate = new(2026, 12, 31, 0, 0, 0, DateTimeKind.Unspecified);
    private const string ReasonCode = "OPENING_BALANCE";

    private readonly IWarehouseCodeProvider _warehouseCode;
    private readonly WarehouseDbContext _db;
    private readonly IDocumentStore _store;
    private readonly ICurrentUserService _user;

    public OpeningBalancesController(
        IWarehouseCodeProvider warehouseCode, WarehouseDbContext db, IDocumentStore store, ICurrentUserService user)
    {
        _warehouseCode = warehouseCode;
        _db = db;
        _store = store;
        _user = user;
    }

    [HttpPost("preview")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> PreviewAsync(IFormFile? file, CancellationToken ct)
    {
        var bytes = await ReadAsync(file, ct);
        return bytes is null ? BadRequest(new { detail = "A CSV file is required." }) : Ok(await BuildPreviewAsync(bytes, ct));
    }

    [HttpPost("confirm")]
    [RequestSizeLimit(20_000_000)]
    public async Task<IActionResult> ConfirmAsync(IFormFile? file, CancellationToken ct)
    {
        var bytes = await ReadAsync(file, ct);
        if (bytes is null)
        {
            return BadRequest(new { detail = "A CSV file is required." });
        }

        var preview = await BuildPreviewAsync(bytes, ct);
        if (preview.AlreadyLoaded)
        {
            var existing = await LoadAsync(preview.FileHash, ct);
            return Ok(ToResult(existing!, alreadyLoaded: true));
        }

        if (preview.FileError is not null || preview.ErrorRows > 0 || preview.Rows.Count == 0)
        {
            return BadRequest(new { detail = "The file has validation errors; nothing was loaded." });
        }

        if (await LKvitai.MES.Modules.Warehouse.Infrastructure.MonthEnd.AccountingPeriods.CheckAsync(_db, ct, DateOnly.FromDateTime(OpeningDate)) is { } periodClosed)
        {
            return BadRequest(new { detail = periodClosed.Text, code = periodClosed.Code });
        }

        var lines = await ResolveLinesAsync(preview, ct);
        var doc = new OpeningBalanceDocument
        {
            Id = preview.FileHash,
            Number = "OB-2026-12-31-" + preview.FileHash[..8].ToUpperInvariant(),
            FileName = file!.FileName,
            BusinessDate = OpeningDate,
            CreatedAtUtc = DateTime.UtcNow,
            CreatedBy = _user.GetCurrentUserId(),
            Lines = lines,
            Totals = preview.Totals,
            TotalQty = lines.Sum(x => x.Qty),
            TotalValueEur = lines.Sum(x => x.ValueEur)
        };

        var warehouse = _warehouseCode.Code;
        var lineLocations = lines.Select(x => x.Location).Distinct().ToList();
        var locationIds = await _db.Locations.AsNoTracking()
            .Where(l => lineLocations.Contains(l.Code))
            .ToDictionaryAsync(l => l.Code, l => l.Id, StringComparer.OrdinalIgnoreCase, ct);
        var now = DateTime.UtcNow;
        var requests = lines.Select(l => new StockValueRequest(
            l.Sku, StockValueKinds.Opening, l.Qty, true, l.ValueEur,
            new StockValueDocument("OpeningBalance", doc.Id, doc.Number, "Opening balance " + doc.BusinessDate.ToString("yyyy-MM-dd"), l.StockAccountProfileId),
            OpeningDate)).ToList();

        IReadOnlyList<StockValueChanged> valueEvents = [];
        try
        {
            await StockValueRetry.RunAsync(async () =>
            {
                await using var session = _store.LightweightSession();
                session.Insert(doc);
                var n = 0;
                foreach (var l in lines)
                {
                    var id = new Guid(MD5.HashData(Encoding.UTF8.GetBytes($"{doc.Id}:{n++}")));
                    session.Events.Append($"stock-adjustment:{id:N}", new StockAdjustedEvent
                    {
                        AggregateId = id,
                        UserId = doc.CreatedBy,
                        TraceId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
                        WarehouseId = warehouse,
                        AdjustmentId = id,
                        ItemId = l.ItemId,
                        SKU = l.Sku,
                        LocationId = locationIds.TryGetValue(l.Location, out var lid) ? lid : null,
                        Location = l.Location,
                        QtyDelta = l.Qty,
                        ReasonCode = ReasonCode,
                        Notes = doc.Number,
                        Timestamp = now
                    });
                }

                valueEvents = await StockValueWriter.AppendAsync(session, _db, requests, ct);
                await session.SaveChangesAsync(ct);
            }, ct);
        }
        catch (Marten.Exceptions.DocumentAlreadyExistsException)
        {
            var existing = await LoadAsync(doc.Id, ct);
            return Ok(ToResult(existing!, alreadyLoaded: true));
        }

        await StockValueWriter.ProjectAsync(
            _db, Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance, valueEvents, ct);
        return Ok(ToResult(doc, alreadyLoaded: false));
    }

    [HttpGet]
    public async Task<IActionResult> ListAsync(CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        var docs = await Marten.QueryableExtensions.ToListAsync(
            session.Query<OpeningBalanceDocument>().OrderByDescending(x => x.CreatedAtUtc), ct);
        return Ok(docs.Select(d => new
        {
            d.Number, d.FileName, d.BusinessDate, d.CreatedAtUtc, d.CreatedBy,
            Lines = d.Lines.Count, d.TotalQty, d.TotalValueEur, d.Totals
        }));
    }

    private static async Task<byte[]?> ReadAsync(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            return null;
        }

        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        return ms.ToArray();
    }

    private async Task<OpeningBalanceDocument?> LoadAsync(string id, CancellationToken ct)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<OpeningBalanceDocument>(id, ct);
    }

    private static OpeningBalanceConfirmResult ToResult(OpeningBalanceDocument d, bool alreadyLoaded) => new()
    {
        AlreadyLoaded = alreadyLoaded, Number = d.Number, Lines = d.Lines.Count,
        TotalQty = d.TotalQty, TotalValueEur = d.TotalValueEur, Totals = d.Totals
    };

    private sealed record ItemInfo(int Id, string Sku, string Name, int CategoryId);

    private async Task<(Dictionary<string, ItemInfo> Items, HashSet<string> Locations, Func<int, (int? Id, string Account, string Name)> Profile)>
        LoadReferenceAsync(IEnumerable<OpeningCsvRow> rows, CancellationToken ct)
    {
        var skus = rows.Select(r => r.Sku).Where(s => s.Length > 0).Distinct().ToList();
        var items = (await _db.Items.AsNoTracking().Where(i => skus.Contains(i.InternalSKU))
                .Select(i => new ItemInfo(i.Id, i.InternalSKU, i.Name, i.CategoryId)).ToListAsync(ct))
            .ToDictionary(i => i.Sku, StringComparer.OrdinalIgnoreCase);
        var locs = rows.Select(r => r.Location).Where(s => s.Length > 0).Distinct().ToList();
        var locations = (await _db.Locations.AsNoTracking().Where(l => locs.Contains(l.Code)).Select(l => l.Code).ToListAsync(ct))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var categories = await _db.ItemCategories.AsNoTracking().ToDictionaryAsync(c => c.Id, ct);
        var profiles = await _db.StockAccountProfiles.AsNoTracking().ToListAsync(ct);
        var def = profiles.FirstOrDefault(p => p.IsDefault);

        (int? Id, string Account, string Name) Resolve(int categoryId)
        {
            int? pid = null;
            for (var id = (int?)categoryId; id is not null && categories.TryGetValue(id.Value, out var c); id = c.ParentCategoryId)
            {
                if (c.StockAccountProfileId is not null) { pid = c.StockAccountProfileId; break; }
            }

            var p = (pid is null ? def : profiles.FirstOrDefault(x => x.Id == pid)) ?? def;
            return p is null ? (null, string.Empty, string.Empty) : (p.Id, p.StockAccount, p.Name);
        }

        return (items, locations, Resolve);
    }

    private async Task<OpeningBalancePreviewResult> BuildPreviewAsync(byte[] bytes, CancellationToken ct)
    {
        var hash = OpeningBalanceCsv.Hash(bytes);
        var result = new OpeningBalancePreviewResult { FileHash = hash, BusinessDate = OpeningDate };
        var existing = await LoadAsync(hash, ct);
        if (existing is not null)
        {
            result.AlreadyLoaded = true;
            result.ExistingNumber = existing.Number;
        }

        var (rows, fileError) = OpeningBalanceCsv.Parse(bytes);
        result.FileError = fileError;
        if (fileError is not null)
        {
            return result;
        }

        var (items, locations, profile) = await LoadReferenceAsync(rows, ct);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in rows)
        {
            var row = new OpeningBalancePreviewRow
            {
                RowNumber = r.RowNumber, Sku = r.Sku, Location = r.Location, Qty = r.Qty, ValueEur = r.ValueEur,
                Errors = [.. r.Errors]
            };
            if (r.Sku.Length > 0)
            {
                if (items.TryGetValue(r.Sku, out var item))
                {
                    row.ItemName = item.Name;
                    row.StockAccount = profile(item.CategoryId).Account;
                }
                else row.Errors.Add("UnknownItem");
            }

            if (r.Location.Length > 0)
            {
                if (!locations.Contains(r.Location)) row.Errors.Add("UnknownLocation");
                else if (StockBoundary.IsOutside(r.Location)) row.Errors.Add("ExternalLocation");
            }

            if (r.Qty is 0m && !r.Errors.Contains(OpeningBalanceCsv.ErrNegativeQty)) row.Errors.Add("ZeroQty");
            if (r.Sku.Length > 0 && r.Location.Length > 0 && !seen.Add($"{r.Sku}|{r.Location}")) row.Errors.Add("DuplicateRow");
            result.Rows.Add(row);
        }

        result.ErrorRows = result.Rows.Count(r => r.Errors.Count > 0);
        result.Totals = result.Rows.Where(r => r.Errors.Count == 0)
            .Select(r => (Row: r, P: profile(items[r.Sku].CategoryId)))
            .GroupBy(x => x.P.Id)
            .Select(g => new OpeningBalanceAccountTotal
            {
                StockAccountProfileId = g.Key,
                StockAccount = g.First().P.Account,
                ProfileName = g.First().P.Name,
                Rows = g.Count(),
                Qty = g.Sum(x => x.Row.Qty ?? 0m),
                ValueEur = g.Sum(x => Math.Round(x.Row.ValueEur ?? 0m, 2, MidpointRounding.AwayFromZero))
            }).OrderBy(t => t.StockAccount).ToList();
        return result;
    }

    private async Task<List<OpeningBalanceLine>> ResolveLinesAsync(OpeningBalancePreviewResult preview, CancellationToken ct)
    {
        var rows = preview.Rows.Select(r => new OpeningCsvRow(r.RowNumber, r.Sku, r.Location, r.Qty, r.ValueEur, [])).ToList();
        var (items, _, profile) = await LoadReferenceAsync(rows, ct);
        return preview.Rows.Select(r =>
        {
            var item = items[r.Sku];
            var p = profile(item.CategoryId);
            return new OpeningBalanceLine
            {
                Sku = item.Sku, ItemId = item.Id, Location = r.Location,
                Qty = r.Qty!.Value, ValueEur = Math.Round(r.ValueEur!.Value, 2, MidpointRounding.AwayFromZero),
                StockAccountProfileId = p.Id, StockAccount = p.Account
            };
        }).ToList();
    }
}
