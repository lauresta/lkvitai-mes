using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Api.Configuration;

/// <summary>Logs a warning at startup when the configured code is not an active physical warehouse.</summary>
public sealed class WarehouseCodeStartupCheck : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IWarehouseCodeProvider _warehouse;
    private readonly ILogger<WarehouseCodeStartupCheck> _logger;

    public WarehouseCodeStartupCheck(
        IServiceScopeFactory scopeFactory,
        IWarehouseCodeProvider warehouse,
        ILogger<WarehouseCodeStartupCheck> logger)
    {
        _scopeFactory = scopeFactory;
        _warehouse = warehouse;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            var code = _warehouse.Code;
            var ok = await db.Warehouses.AsNoTracking()
                .AnyAsync(x => x.Code == code && !x.IsVirtual && x.Status == "Active", cancellationToken);
            if (!ok)
                _logger.LogWarning(
                    "Configured Warehouse:Code '{WarehouseCode}' is not an active physical warehouse; stock operations will not find master data for it.",
                    code);
            else
                _logger.LogInformation("Warehouse code '{WarehouseCode}' is configured.", code);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Could not verify configured Warehouse:Code at startup.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
