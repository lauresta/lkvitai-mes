using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

public sealed class AgnumBalanceImportService : IAgnumBalanceImportService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IAgnumApiClientFactory _apiClientFactory;
    private readonly ILogger<AgnumBalanceImportService> _logger;

    public AgnumBalanceImportService(
        WarehouseDbContext dbContext,
        IAgnumApiClientFactory apiClientFactory,
        ILogger<AgnumBalanceImportService> logger)
    {
        _dbContext = dbContext;
        _apiClientFactory = apiClientFactory;
        _logger = logger;
    }

    public async Task<Guid> StartImportAsync(int sndId, CancellationToken ct = default)
    {
        var run = new AgnumBalanceImportRun
        {
            SndId = sndId,
            StartedAt = DateTime.UtcNow,
            Status = "Running"
        };

        _dbContext.AgnumBalanceImportRuns.Add(run);
        await _dbContext.SaveChangesAsync(ct);

        try
        {
            var client = _apiClientFactory.GetForSndId(sndId);
            var products = await client.GetProductsAsync(ct);
            var links = await _dbContext.AgnumProductLinks
                .Where(x => x.SndId == sndId)
                .Include(x => x.Item)
                .ToDictionaryAsync(x => x.AgnumProductId, x => x, ct);

            var balanceCount = 0;
            foreach (var product in products)
            {
                var existing = await _dbContext.AgnumVirtualWarehouseBalances
                    .FirstOrDefaultAsync(x => x.ImportRunId == run.Id && x.SndId == sndId && x.AgnumProductId == product.Id, ct);

                var link = links.TryGetValue(product.Id, out var productLink) ? productLink : null;
                if (existing is null)
                {
                    existing = new AgnumVirtualWarehouseBalance
                    {
                        ImportRunId = run.Id,
                        SndId = sndId,
                        AgnumProductId = product.Id
                    };
                    _dbContext.AgnumVirtualWarehouseBalances.Add(existing);
                }

                existing.ItemId = link?.ItemId;
                existing.Sku = link?.Item?.InternalSKU;
                existing.Quantity = product.Balance;
                existing.Uom = product.Pcs;
                existing.ImportedAt = DateTime.UtcNow;
                existing.SourceHash = ComputeRawHash(product);

                balanceCount++;
            }

            run.ProductCount = products.Count;
            run.BalanceCount = balanceCount;
            run.ErrorCount = 0;
            run.Status = "Completed";
            run.FinishedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);

            return run.Id;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Agnum balance import failed for sndId {SndId}.", sndId);
            run.ErrorCount = 1;
            run.Status = "Failed";
            run.ErrorSummary = ex.Message;
            run.FinishedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(ct);
            return run.Id;
        }
    }

    public async Task<AgnumBalanceImportRunStatus> GetRunStatusAsync(Guid runId, CancellationToken ct = default)
    {
        var run = await _dbContext.AgnumBalanceImportRuns
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == runId, ct);

        if (run is null)
        {
            throw new KeyNotFoundException($"Agnum balance import run '{runId}' not found.");
        }

        return new AgnumBalanceImportRunStatus
        {
            RunId = run.Id,
            SndId = run.SndId,
            Status = run.Status,
            ProductCount = run.ProductCount,
            BalanceCount = run.BalanceCount,
            ErrorCount = run.ErrorCount,
            ErrorSummary = run.ErrorSummary,
            StartedAt = run.StartedAt,
            FinishedAt = run.FinishedAt
        };
    }

    private static string ComputeRawHash(AgnumProductDto product)
    {
        var text = string.Join("|",
            product.Id,
            product.Code ?? string.Empty,
            product.Name ?? string.Empty,
            product.Enabled,
            product.Pcs ?? string.Empty,
            product.Netto?.ToString("G29") ?? string.Empty,
            product.Brutto?.ToString("G29") ?? string.Empty,
            string.Join(";", product.Barcodes ?? Enumerable.Empty<string>()));

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
