using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Integration;

public class ElectronicSignatureIntegrationTests
{
    [Fact]
    [Trait("Category", "Signatures")]
    public async Task CaptureAndVerify_ShouldValidateHashChain()
    {
        await using var db = new WarehouseDbContext(new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var service = new ElectronicSignatureService(db, NullLogger<ElectronicSignatureService>.Instance);

        await service.CaptureAsync(new CaptureSignatureCommand(
            "QC_APPROVAL",
            "LOT-INT-1",
            "QA User",
            "APPROVED",
            "qa-user",
            "127.0.0.1",
            "pw"));

        var verify = await service.VerifyHashChainAsync();

        verify.Valid.Should().BeTrue();
        verify.SignatureCount.Should().Be(1);
    }
}
