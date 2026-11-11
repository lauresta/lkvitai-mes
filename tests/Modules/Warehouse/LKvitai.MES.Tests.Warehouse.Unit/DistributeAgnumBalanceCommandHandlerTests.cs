using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using FluentAssertions;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class DistributeAgnumBalanceCommandHandlerTests
{
    [Fact]
    public async Task DistributeAgnumBalance_SendsReceiveGoodsCommand_WhenSkuLinked()
    {
        await using var db = CreateDbContext();
        var balance = await SeedVirtualBalanceAsync(db);
        var mediator = new Mock<IMediator>();
        ReceiveGoodsCommand? sentCommand = null;

        mediator
            .Setup(x => x.Send(It.IsAny<ReceiveGoodsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => sentCommand = (ReceiveGoodsCommand)request)
            .ReturnsAsync(Result.Ok());

        var handler = CreateHandler(db, mediator);

        var result = await handler.Handle(CreateCommand(balance.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        sentCommand.Should().NotBeNull();
        sentCommand!.FromLocation.Should().Be("AGNUM");
        sentCommand.WarehouseId.Should().Be("WH-1");
        sentCommand.Location.Should().Be("A-01");
        sentCommand.HuType.Should().Be("PALLET");
        sentCommand.Lines.Should().ContainSingle();
        sentCommand.Lines[0].SKU.Should().Be("SKU-001");
        sentCommand.Lines[0].Quantity.Should().Be(7m);
        mediator.Verify(
            x => x.Send(It.IsAny<RecordStockMovementCommand>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task DistributeAgnumBalance_ReturnsFailure_WhenReceiveGoodsFails()
    {
        await using var db = CreateDbContext();
        var balance = await SeedVirtualBalanceAsync(db);
        var mediator = new Mock<IMediator>();

        mediator
            .Setup(x => x.Send(It.IsAny<ReceiveGoodsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(DomainErrorCodes.ReceiveGoodsFailed));

        var handler = CreateHandler(db, mediator);

        var result = await handler.Handle(CreateCommand(balance.Id), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrorCodes.ReceiveGoodsFailed);
        (await db.AgnumBalanceDistributions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DistributeAgnumBalance_SavesDistributionRecord_WhenReceiveGoodsSucceeds()
    {
        await using var db = CreateDbContext();
        var balance = await SeedVirtualBalanceAsync(db);
        var mediator = new Mock<IMediator>();
        Guid huCommandId = Guid.Empty;

        mediator
            .Setup(x => x.Send(It.IsAny<ReceiveGoodsCommand>(), It.IsAny<CancellationToken>()))
            .Callback<object, CancellationToken>((request, _) => huCommandId = ((ReceiveGoodsCommand)request).CommandId)
            .ReturnsAsync(Result.Ok());

        var handler = CreateHandler(db, mediator);

        var result = await handler.Handle(CreateCommand(balance.Id), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var distribution = await db.AgnumBalanceDistributions.SingleAsync();
        distribution.VirtualBalanceId.Should().Be(balance.Id);
        distribution.SndId.Should().Be(balance.SndId);
        distribution.AgnumProductId.Should().Be(balance.AgnumProductId);
        distribution.Sku.Should().Be("SKU-001");
        distribution.LocationCode.Should().Be("A-01");
        distribution.WarehouseId.Should().Be("WH-1");
        distribution.Quantity.Should().Be(7m);
        distribution.StockMovementCommandId.Should().Be(huCommandId);
        distribution.DistributedBy.Should().Be(OperatorId.ToString());
        distribution.DistributedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task DistributeEndpoint_ReturnsBadRequest_AndWritesNothing_WhenWarehouseIsNotTheConfiguredOne()
    {
        await using var db = CreateDbContext();
        var balance = await SeedVirtualBalanceAsync(db);
        var receive = new Mock<IMediator>(MockBehavior.Strict);
        var handler = CreateHandler(db, receive);
        var mediator = new Mock<IMediator>();
        mediator.Setup(x => x.Send(It.IsAny<DistributeAgnumBalanceCommand>(), It.IsAny<CancellationToken>()))
            .Returns<IRequest<Result>, CancellationToken>((command, ct) => handler.Handle((DistributeAgnumBalanceCommand)command, ct));
        var controller = new LKvitai.MES.Modules.Warehouse.Api.Controllers.AgnumImportController(
            db, null!, null!, null!, null!, mediator.Object);

        var response = await controller.DistributeBalanceAsync(balance.Id,
            new LKvitai.MES.Modules.Warehouse.Api.Controllers.AgnumImportController.DistributeAgnumBalanceRequest(
                "A-01", "OTHER", 7m, OperatorId));

        response.Should().BeOfType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>();
        (await db.AgnumBalanceDistributions.CountAsync()).Should().Be(0);
        receive.VerifyNoOtherCalls();
    }

    private static readonly Guid OperatorId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static DistributeAgnumBalanceCommand CreateCommand(Guid virtualBalanceId)
        => new()
        {
            CommandId = Guid.NewGuid(),
            VirtualBalanceId = virtualBalanceId,
            WarehouseId = " WH-1 ",
            LocationCode = " A-01 ",
            Quantity = 7m,
            OperatorId = OperatorId
        };

    private static DistributeAgnumBalanceCommandHandler CreateHandler(
        WarehouseDbContext db,
        Mock<IMediator> mediator)
        => new(
            db,
            mediator.Object,
            Mock.Of<ILogger<DistributeAgnumBalanceCommandHandler>>(), WarehouseCodeProvider.For("WH-1"));

    private static async Task<AgnumVirtualWarehouseBalance> SeedVirtualBalanceAsync(WarehouseDbContext db)
    {
        var run = new AgnumBalanceImportRun
        {
            Id = Guid.NewGuid(),
            SndId = 493,
            StartedAt = DateTime.UtcNow,
            Status = "Completed"
        };

        var balance = new AgnumVirtualWarehouseBalance
        {
            Id = Guid.NewGuid(),
            ImportRunId = run.Id,
            SndId = run.SndId,
            AgnumProductId = 1001,
            ItemId = 17,
            Sku = " SKU-001 ",
            Quantity = 20m,
            Uom = "m",
            ImportedAt = DateTime.UtcNow
        };

        db.AgnumBalanceImportRuns.Add(run);
        db.AgnumVirtualWarehouseBalances.Add(balance);
        await db.SaveChangesAsync();
        return balance;
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new WarehouseDbContext(options);
    }
}
