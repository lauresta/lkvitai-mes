using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.BackgroundJobs;

public sealed class AgnumImportRecurringJob
{
    public const string JobId = "agnum-daily-import";

    private readonly WarehouseDbContext _dbContext;
    private readonly IAgnumNomenclatureImportService _nomenclatureImportService;
    private readonly IAgnumBalanceImportService _balanceImportService;
    private readonly ILogger<AgnumImportRecurringJob> _logger;

    public AgnumImportRecurringJob(
        WarehouseDbContext dbContext,
        IAgnumNomenclatureImportService nomenclatureImportService,
        IAgnumBalanceImportService balanceImportService,
        ILogger<AgnumImportRecurringJob> logger)
    {
        _dbContext = dbContext;
        _nomenclatureImportService = nomenclatureImportService;
        _balanceImportService = balanceImportService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;
        var mappings = await _dbContext.AgnumWarehouseMappings
            .AsNoTracking()
            .Where(x => x.IsImportEnabled)
            .OrderBy(x => x.SndId)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Starting Agnum daily import for {Count} enabled warehouses.", mappings.Count);

        var succeeded = 0;
        var failed = 0;

        foreach (var mapping in mappings)
        {
            try
            {
                var warehouseStartedAt = DateTimeOffset.UtcNow;
                _logger.LogInformation(
                    "Agnum import started for sndId {SndId} ({AgnumName}).",
                    mapping.SndId,
                    mapping.AgnumName);

                _logger.LogInformation("Agnum nomenclature import started for sndId {SndId}.", mapping.SndId);
                var productResult = await _nomenclatureImportService.ApplyAsync(mapping.SndId, cancellationToken);
                _logger.LogInformation(
                    "Agnum nomenclature import completed for sndId {SndId}. Created={Created} Updated={Updated} Skipped={Skipped} Conflicts={Conflicts}",
                    mapping.SndId,
                    productResult.Created,
                    productResult.Updated,
                    productResult.Skipped,
                    productResult.Conflicts.Count);

                _logger.LogInformation("Agnum balance import started for sndId {SndId}.", mapping.SndId);
                var balanceRunId = await _balanceImportService.StartImportAsync(mapping.SndId, cancellationToken);

                succeeded++;
                _logger.LogInformation(
                    "Agnum import completed for sndId {SndId} in {ElapsedMs} ms. Created={Created} Updated={Updated} Skipped={Skipped} Conflicts={Conflicts} BalanceRunId={BalanceRunId}",
                    mapping.SndId,
                    (DateTimeOffset.UtcNow - warehouseStartedAt).TotalMilliseconds,
                    productResult.Created,
                    productResult.Updated,
                    productResult.Skipped,
                    productResult.Conflicts.Count,
                    balanceRunId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                _logger.LogError(
                    ex,
                    "Agnum import failed for sndId {SndId}; continuing with the next configured warehouse.",
                    mapping.SndId);
            }
        }

        _logger.LogInformation(
            "Agnum daily import finished in {ElapsedMs} ms. Succeeded={Succeeded} Failed={Failed}",
            (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds,
            succeeded,
            failed);
    }
}
