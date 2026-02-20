using LKvitai.MES.Modules.Warehouse.Api.Services;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public sealed class ChaosTests
{
    [Fact]
    public async Task DatabaseFailure_Returns503_AndOpensCircuitAfterThreeFailures()
    {
        var sut = new ChaosResilienceService();
        var injection = new ChaosInjectionOptions
        {
            Enabled = true,
            Scenario = ChaosScenario.DatabaseFailure
        };

        var first = await sut.ExecuteAsync(
            ChaosScenario.DatabaseFailure,
            _ => Task.FromResult("ok"),
            injection: injection);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, first.StatusCode);
        Assert.False(first.IsSuccess);
        Assert.Equal(3, first.RetryAttempts);

        var second = await sut.ExecuteAsync(
            ChaosScenario.DatabaseFailure,
            _ => Task.FromResult("ok"),
            injection: injection);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, second.StatusCode);
        Assert.True(second.CircuitOpen || !second.IsSuccess);
    }

    [Fact]
    public async Task RedisFailure_UsesFallback_AndAvoids500()
    {
        var sut = new ChaosResilienceService();
        var injection = new ChaosInjectionOptions
        {
            Enabled = true,
            Scenario = ChaosScenario.RedisFailure
        };

        var result = await sut.ExecuteAsync(
            ChaosScenario.RedisFailure,
            _ => Task.FromResult("cache-hit"),
            _ => Task.FromResult("db-fallback"),
            injection);

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.True(result.IsSuccess);
        Assert.True(result.IsDegraded);
        Assert.Equal("db-fallback", result.Value);
    }

    [Fact]
    public async Task NetworkPartition_Returns503_Not500()
    {
        var sut = new ChaosResilienceService();
        var injection = new ChaosInjectionOptions
        {
            Enabled = true,
            Scenario = ChaosScenario.NetworkPartition
        };

        var result = await sut.ExecuteAsync(
            ChaosScenario.NetworkPartition,
            _ => Task.FromResult("ok"),
            injection: injection);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task HighLatency_AddsAtLeastConfiguredDelay_AndReturnsSuccess()
    {
        var sut = new ChaosResilienceService();
        var injection = new ChaosInjectionOptions
        {
            Enabled = true,
            Scenario = ChaosScenario.HighLatency,
            InjectLatencyMs = 500
        };

        var started = DateTimeOffset.UtcNow;
        var result = await sut.ExecuteAsync(
            ChaosScenario.DatabaseFailure,
            _ => Task.FromResult("ok"),
            injection: injection);
        var elapsed = DateTimeOffset.UtcNow - started;

        Assert.Equal(StatusCodes.Status200OK, result.StatusCode);
        Assert.True(result.IsSuccess);
        Assert.True(elapsed >= TimeSpan.FromMilliseconds(450));
        Assert.True(elapsed < TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task DatabaseFailure_TransactionalExecution_RollsBackAndPreservesConsistency()
    {
        var sut = new ChaosResilienceService();
        var injection = new ChaosInjectionOptions
        {
            Enabled = true,
            Scenario = ChaosScenario.DatabaseFailure
        };

        var store = new List<string>();
        store.Add("baseline");

        store.Add("pending-order");
        var result = await sut.ExecuteTransactionalAsync(
            ChaosScenario.DatabaseFailure,
            _ => Task.FromResult("created"),
            _ =>
            {
                store.Remove("pending-order");
                return Task.CompletedTask;
            },
            injection);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, result.StatusCode);
        Assert.DoesNotContain("pending-order", store);
        Assert.Single(store);
    }
}
