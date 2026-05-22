using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AgnumReconciliationTests
{
    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public void Reconciliation_Delta_IsPositive_WhenMesStockExceedsDistributed()
    {
        var delta = 12m - 10m;
        var status = AgnumReconciliationStatusCalculator.GetStatus("SKU-001", delta);

        delta.Should().BePositive();
        status.Should().Be("Over");
    }

    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public void Reconciliation_Status_IsNotLinked_WhenSkuIsNull()
    {
        var status = AgnumReconciliationStatusCalculator.GetStatus(null, 0m);

        status.Should().Be("NotLinked");
    }

    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public void Reconciliation_Status_IsMatched_WhenDeltaIsZero()
    {
        var status = AgnumReconciliationStatusCalculator.GetStatus("SKU-001", 0m);

        status.Should().Be("Matched");
    }

    [Fact]
    [Trait("Category", "AgnumReconciliation")]
    public void Reconciliation_Status_IsUnder_WhenMesStockLessThanDistributed()
    {
        var delta = 8m - 10m;
        var status = AgnumReconciliationStatusCalculator.GetStatus("SKU-001", delta);

        status.Should().Be("Under");
    }
}
