using System.Collections.Concurrent;
using System.Globalization;
using CsvHelper;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Services;

public sealed record AgnumReconciliationLine(
    string AccountCode,
    string Sku,
    string ItemName,
    decimal WarehouseQty,
    decimal WarehouseCost,
    decimal WarehouseValue,
    decimal AgnumBalance,
    decimal Variance,
    decimal VariancePercent);

public sealed record AgnumReconciliationSummary(
    decimal TotalVariance,
    int ItemsWithVariance,
    string? LargestVarianceSku,
    decimal LargestVarianceAmount);

public sealed record AgnumReconciliationReport(
    Guid ReportId,
    DateOnly ReportDate,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<AgnumReconciliationLine> Lines,
    AgnumReconciliationSummary Summary);

public interface IAgnumReconciliationReportStore
{
    void Save(AgnumReconciliationReport report);

    bool TryGet(Guid reportId, out AgnumReconciliationReport? report);
}

public sealed class InMemoryAgnumReconciliationReportStore : IAgnumReconciliationReportStore
{
    private readonly ConcurrentDictionary<Guid, AgnumReconciliationReport> _reports = new();

    public void Save(AgnumReconciliationReport report)
    {
        _reports[report.ReportId] = report;
    }

    public bool TryGet(Guid reportId, out AgnumReconciliationReport? report)
    {
        if (_reports.TryGetValue(reportId, out var stored))
        {
            report = stored;
            return true;
        }

        report = null;
        return false;
    }
}

public interface IAgnumReconciliationService
{
    Task<AgnumReconciliationReport> GenerateAsync(
        DateOnly reportDate,
        Stream agnumBalanceCsvStream,
        CancellationToken cancellationToken = default);

    AgnumReconciliationReport ApplyFilters(
        AgnumReconciliationReport report,
        string? accountCode,
        decimal? varianceThresholdAmount,
        decimal? varianceThresholdPercent);
}

public sealed class AgnumReconciliationService : IAgnumReconciliationService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ILogger<AgnumReconciliationService> _logger;

    public AgnumReconciliationService(
        WarehouseDbContext dbContext,
        ILogger<AgnumReconciliationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<AgnumReconciliationReport> GenerateAsync(
        DateOnly reportDate,
        Stream agnumBalanceCsvStream,
        CancellationToken cancellationToken = default)
    {
        var exportHistory = await FindExportHistoryAsync(reportDate, cancellationToken);
        if (exportHistory is null)
        {
            throw new InvalidOperationException(
                $"No successful Agnum export history found for date {reportDate:yyyy-MM-dd}.");
        }

        if (string.IsNullOrWhiteSpace(exportHistory.FilePath) || !File.Exists(exportHistory.FilePath))
        {
            throw new InvalidOperationException(
                $"Export file not found for history {exportHistory.Id}: '{exportHistory.FilePath ?? "(null)"}'.");
        }

        var warehouseRows = await ReadWarehouseRowsAsync(exportHistory.FilePath, cancellationToken);
        var agnumRows = await ReadAgnumRowsAsync(agnumBalanceCsvStream, cancellationToken);

        var agnumBySku = agnumRows
            .GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                x => x.Key,
                x => decimal.Round(x.Sum(y => y.Balance), 4, MidpointRounding.AwayFromZero),
                StringComparer.OrdinalIgnoreCase);

        var lines = warehouseRows
            .GroupBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var sku = group.Key;
                var qty = group.Sum(x => x.Quantity);
                var value = decimal.Round(group.Sum(x => x.OnHandValue), 4, MidpointRounding.AwayFromZero);
                var unitCost = qty == 0m
                    ? 0m
                    : decimal.Round(value / qty, 4, MidpointRounding.AwayFromZero);

                var agnumBalance = agnumBySku.TryGetValue(sku, out var balance)
                    ? balance
                    : 0m;

                var variance = decimal.Round(value - agnumBalance, 4, MidpointRounding.AwayFromZero);
                var variancePercent = agnumBalance == 0m
                    ? (value == 0m ? 0m : 100m)
                    : decimal.Round((variance / agnumBalance) * 100m, 4, MidpointRounding.AwayFromZero);

                return new AgnumReconciliationLine(
                    group.Select(x => x.AccountCode).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? "UNKNOWN",
                    sku,
                    group.Select(x => x.ItemName).FirstOrDefault(x => !string.IsNullOrWhiteSpace(x)) ?? sku,
                    qty,
                    unitCost,
                    value,
                    agnumBalance,
                    variance,
                    variancePercent);
            })
            .OrderByDescending(x => Math.Abs(x.Variance))
            .ThenBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "Generated Agnum reconciliation report for {Date} with {LineCount} lines",
            reportDate,
            lines.Count);

        return BuildReport(
            Guid.NewGuid(),
            reportDate,
            DateTimeOffset.UtcNow,
            lines);
    }

    public AgnumReconciliationReport ApplyFilters(
        AgnumReconciliationReport report,
        string? accountCode,
        decimal? varianceThresholdAmount,
        decimal? varianceThresholdPercent)
    {
        IEnumerable<AgnumReconciliationLine> query = report.Lines;

        if (!string.IsNullOrWhiteSpace(accountCode))
        {
            var normalizedCode = accountCode.Trim();
            query = query.Where(x => string.Equals(x.AccountCode, normalizedCode, StringComparison.OrdinalIgnoreCase));
        }

        var hasAmount = varianceThresholdAmount.HasValue && varianceThresholdAmount.Value > 0m;
        var hasPercent = varianceThresholdPercent.HasValue && varianceThresholdPercent.Value > 0m;

        if (hasAmount || hasPercent)
        {
            query = query.Where(x =>
                (hasAmount && Math.Abs(x.Variance) > varianceThresholdAmount!.Value) ||
                (hasPercent && Math.Abs(x.VariancePercent) > varianceThresholdPercent!.Value));
        }

        var filtered = query
            .OrderByDescending(x => Math.Abs(x.Variance))
            .ThenBy(x => x.Sku, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return BuildReport(
            report.ReportId,
            report.ReportDate,
            report.GeneratedAt,
            filtered);
    }

    private static AgnumReconciliationReport BuildReport(
        Guid reportId,
        DateOnly reportDate,
        DateTimeOffset generatedAt,
        IReadOnlyList<AgnumReconciliationLine> lines)
    {
        var totalVariance = decimal.Round(lines.Sum(x => x.Variance), 4, MidpointRounding.AwayFromZero);
        var itemsWithVariance = lines.Count(x => Math.Abs(x.Variance) > 0.01m);
        var largest = lines
            .OrderByDescending(x => Math.Abs(x.Variance))
            .FirstOrDefault();

        return new AgnumReconciliationReport(
            reportId,
            reportDate,
            generatedAt,
            lines,
            new AgnumReconciliationSummary(
                totalVariance,
                itemsWithVariance,
                largest?.Sku,
                largest is null ? 0m : decimal.Round(Math.Abs(largest.Variance), 4, MidpointRounding.AwayFromZero)));
    }

    private async Task<AgnumExportHistory?> FindExportHistoryAsync(
        DateOnly reportDate,
        CancellationToken cancellationToken)
    {
        var fromUtc = new DateTimeOffset(reportDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        var toUtc = fromUtc.AddDays(1);

        return await _dbContext.AgnumExportHistories
            .AsNoTracking()
            .Where(x =>
                x.Status == AgnumExportStatus.Success &&
                !string.IsNullOrWhiteSpace(x.FilePath) &&
                x.ExportedAt >= fromUtc &&
                x.ExportedAt < toUtc)
            .OrderByDescending(x => x.ExportedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static async Task<List<WarehouseExportRow>> ReadWarehouseRowsAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var rows = new List<WarehouseExportRow>();

        await csv.ReadAsync();
        csv.ReadHeader();

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rows.Add(new WarehouseExportRow(
                csv.GetField("AccountCode") ?? string.Empty,
                csv.GetField("SKU") ?? string.Empty,
                csv.GetField("ItemName") ?? string.Empty,
                ParseDecimal(csv.GetField("Quantity")),
                ParseDecimal(csv.GetField("UnitCost")),
                ParseDecimal(csv.GetField("OnHandValue"))));
        }

        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x.Sku))
            .ToList();
    }

    private static async Task<List<AgnumBalanceRow>> ReadAgnumRowsAsync(
        Stream csvStream,
        CancellationToken cancellationToken)
    {
        csvStream.Position = 0;
        using var reader = new StreamReader(csvStream, leaveOpen: true);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        var rows = new List<AgnumBalanceRow>();

        if (!await csv.ReadAsync())
        {
            throw new InvalidOperationException("Agnum balance CSV is empty.");
        }

        csv.ReadHeader();
        var headers = csv.HeaderRecord ?? Array.Empty<string>();
        if (headers.Length == 0)
        {
            throw new InvalidOperationException("Agnum balance CSV must contain a header row.");
        }

        var skuHeader = ResolveHeader(headers, "SKU", "Sku", "ItemSku", "ItemSKU");
        var balanceHeader = ResolveHeader(headers, "AgnumBalance", "Balance", "Amount", "Value");

        if (skuHeader is null || balanceHeader is null)
        {
            throw new InvalidOperationException(
                "Agnum balance CSV must include columns for SKU and AgnumBalance (or Balance/Amount/Value).");
        }

        while (await csv.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sku = (csv.GetField(skuHeader) ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(sku))
            {
                continue;
            }

            var balance = ParseDecimal(csv.GetField(balanceHeader));
            rows.Add(new AgnumBalanceRow(sku, balance));
        }

        if (rows.Count == 0)
        {
            throw new InvalidOperationException("Agnum balance CSV does not contain any data rows.");
        }

        return rows;
    }

    private static string? ResolveHeader(IReadOnlyList<string> headers, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var match = headers.FirstOrDefault(x => string.Equals(x, candidate, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match))
            {
                return match;
            }
        }

        return null;
    }

    private static decimal ParseDecimal(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return 0m;
        }

        var normalized = raw.Trim().Replace(",", string.Empty, StringComparison.Ordinal);
        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (decimal.TryParse(normalized, NumberStyles.Any, CultureInfo.CurrentCulture, out parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Unable to parse decimal value '{raw}'.");
    }

    private sealed record WarehouseExportRow(
        string AccountCode,
        string Sku,
        string ItemName,
        decimal Quantity,
        decimal UnitCost,
        decimal OnHandValue);

    private sealed record AgnumBalanceRow(
        string Sku,
        decimal Balance);
}
