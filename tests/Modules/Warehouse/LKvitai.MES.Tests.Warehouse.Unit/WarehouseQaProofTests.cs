using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.DataProtection;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class WarehouseQaProofTests
{
    [Fact]
    public void Proof_is_purpose_bound_and_expires()
    {
        var provider = DataProtectionProvider.Create(new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), "warehouse-qa-proof-" + Guid.NewGuid())));
        var clock = new TestClock(new DateTimeOffset(2026, 11, 10, 18, 0, 0, TimeSpan.Zero));
        var proof = new WarehouseQaProof(provider, clock);
        var token = proof.Create(clock.GetUtcNow().AddMinutes(10));

        Assert.True(proof.IsValid(token));
        Assert.False(proof.IsValid(token + "tampered"));
        Assert.False(proof.IsValid(token.Replace(WarehouseQaProof.Prefix, "other:", StringComparison.Ordinal)));

        clock.Now = clock.Now.AddMinutes(11);
        Assert.False(proof.IsValid(token));
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
