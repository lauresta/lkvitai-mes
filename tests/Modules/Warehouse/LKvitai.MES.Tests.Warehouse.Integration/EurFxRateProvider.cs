using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;

namespace LKvitai.MES.Tests.Warehouse.Integration;

/// <summary>Test double: every currency costs one euro per unit; no network.</summary>
internal sealed class EurFxRateProvider : IFxRateProvider
{
    public Task<FxRateResult> GetRateAsync(string currency, DateOnly date, CancellationToken ct = default)
        => Task.FromResult(FxRateResult.Ok(new FxRateQuote(1m, date, "Test")));

    public Task<decimal?> GetPreviousRateAsync(string currency, DateOnly date, CancellationToken ct = default)
        => Task.FromResult<decimal?>(null);
}
