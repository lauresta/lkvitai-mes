using FluentAssertions;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class ElectronicSignatureServiceTests
{
    [Fact]
    [Trait("Category", "Signatures")]
    public async Task CaptureAsync_ShouldLinkHashChain()
    {
        await using var db = CreateDb();
        var sut = new ElectronicSignatureService(db, NullLoggerFactory.Instance.CreateLogger<ElectronicSignatureService>());

        var first = await sut.CaptureAsync(new CaptureSignatureCommand(
            "QC_APPROVAL",
            "LOT-1",
            "Jane Smith",
            "APPROVED",
            "user-1",
            "127.0.0.1",
            "pw"), CancellationToken.None);

        var second = await sut.CaptureAsync(new CaptureSignatureCommand(
            "COST_ADJUSTMENT",
            "SKU-1",
            "John Doe",
            "APPROVED",
            "user-2",
            "127.0.0.1",
            "pw"), CancellationToken.None);

        first.CurrentHash.Should().NotBeNullOrWhiteSpace();
        second.PreviousHash.Should().Be(first.CurrentHash);
    }

    [Fact]
    [Trait("Category", "Signatures")]
    public async Task VerifyHashChainAsync_WhenIntact_ShouldBeValid()
    {
        await using var db = CreateDb();
        var sut = new ElectronicSignatureService(db, NullLoggerFactory.Instance.CreateLogger<ElectronicSignatureService>());

        await sut.CaptureAsync(new CaptureSignatureCommand(
            "QC_APPROVAL",
            "LOT-1",
            "Jane Smith",
            "APPROVED",
            "user-1",
            "127.0.0.1",
            "pw"), CancellationToken.None);

        var result = await sut.VerifyHashChainAsync();

        result.Valid.Should().BeTrue();
        result.SignatureCount.Should().Be(1);
    }

    private static WarehouseDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new WarehouseDbContext(options);
    }
}
