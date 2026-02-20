using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class TransferCommandHandlersTests
{
    [Fact]
    [Trait("Category", "Transfers")]
    public async Task CreateTransfer_WhenScrapTarget_ShouldSetDraft()
    {
        await using var db = CreateDbContext();
        await SeedItemsAndLocationsAsync(db);

        var bus = new RecordingEventBus();
        var handler = new CreateTransferCommandHandler(
            db,
            bus,
            MockCurrentUser("operator-1"),
            Mock.Of<ILogger<CreateTransferCommandHandler>>());

        var result = await handler.Handle(new CreateTransferCommand
        {
            CommandId = Guid.NewGuid(),
            FromWarehouse = "NLQ",
            ToWarehouse = "SCRAP",
            RequestedBy = "operator-1",
            Lines =
            [
                new TransferLineCommand
                {
                    ItemId = 1,
                    Qty = 10m,
                    FromLocationId = 11,
                    ToLocationId = 12
                }
            ]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var transfer = await db.Transfers.Include(x => x.Lines).SingleAsync();
        transfer.Status.Should().Be(TransferStatus.Draft);
        transfer.TransferNumber.Should().MatchRegex(@"^TRF-\d{8}-\d{3}$");
        transfer.Lines.Should().ContainSingle();
        bus.Published.OfType<TransferCreatedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public async Task CreateTransfer_WhenNonScrapTarget_ShouldSetDraft()
    {
        await using var db = CreateDbContext();
        await SeedItemsAndLocationsAsync(db);

        var bus = new RecordingEventBus();
        var handler = new CreateTransferCommandHandler(
            db,
            bus,
            MockCurrentUser("operator-1"),
            Mock.Of<ILogger<CreateTransferCommandHandler>>());

        var result = await handler.Handle(new CreateTransferCommand
        {
            CommandId = Guid.NewGuid(),
            FromWarehouse = "RES",
            ToWarehouse = "PROD",
            RequestedBy = "operator-1",
            Lines =
            [
                new TransferLineCommand
                {
                    ItemId = 1,
                    Qty = 3m,
                    FromLocationId = 11,
                    ToLocationId = 12
                }
            ]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var transfer = await db.Transfers.SingleAsync();
        transfer.Status.Should().Be(TransferStatus.Draft);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public async Task SubmitTransfer_WhenScrapTarget_ShouldSetPendingApproval()
    {
        await using var db = CreateDbContext();
        var transfer = new Transfer
        {
            TransferNumber = "TRF-0004",
            FromWarehouse = "NLQ",
            ToWarehouse = "SCRAP",
            RequestedBy = "operator-1",
            RequestedAt = DateTimeOffset.UtcNow,
            CreateCommandId = Guid.NewGuid()
        };
        transfer.EnsureRequestedState();
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        var handler = new SubmitTransferCommandHandler(db);
        var result = await handler.Handle(new SubmitTransferCommand
        {
            CommandId = Guid.NewGuid(),
            TransferId = transfer.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await db.Transfers.SingleAsync();
        persisted.Status.Should().Be(TransferStatus.PendingApproval);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public async Task SubmitTransfer_WhenNonScrapTarget_ShouldSetApproved()
    {
        await using var db = CreateDbContext();
        var transfer = new Transfer
        {
            TransferNumber = "TRF-0005",
            FromWarehouse = "RES",
            ToWarehouse = "PROD",
            RequestedBy = "operator-1",
            RequestedAt = DateTimeOffset.UtcNow,
            CreateCommandId = Guid.NewGuid()
        };
        transfer.EnsureRequestedState();
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        var handler = new SubmitTransferCommandHandler(db);
        var result = await handler.Handle(new SubmitTransferCommand
        {
            CommandId = Guid.NewGuid(),
            TransferId = transfer.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await db.Transfers.SingleAsync();
        persisted.Status.Should().Be(TransferStatus.Approved);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public async Task SubmitTransfer_WhenNotDraft_ShouldFail()
    {
        await using var db = CreateDbContext();
        var transfer = new Transfer
        {
            TransferNumber = "TRF-0006",
            FromWarehouse = "RES",
            ToWarehouse = "PROD",
            RequestedBy = "operator-1",
            RequestedAt = DateTimeOffset.UtcNow,
            CreateCommandId = Guid.NewGuid()
        };
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        var handler = new SubmitTransferCommandHandler(db);
        var result = await handler.Handle(new SubmitTransferCommand
        {
            CommandId = Guid.NewGuid(),
            TransferId = transfer.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public async Task ApproveTransfer_WhenUserNotManager_ShouldReturnForbidden()
    {
        await using var db = CreateDbContext();
        var transfer = new Transfer
        {
            TransferNumber = "TRF-0001",
            FromWarehouse = "NLQ",
            ToWarehouse = "SCRAP",
            RequestedBy = "operator-1",
            RequestedAt = DateTimeOffset.UtcNow,
            CreateCommandId = Guid.NewGuid()
        };
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = BuildHttpContextWithRoles()
        };
        var handler = new ApproveTransferCommandHandler(
            db,
            new RecordingEventBus(),
            MockCurrentUser("operator-1"),
            httpContextAccessor);

        var result = await handler.Handle(new ApproveTransferCommand
        {
            CommandId = Guid.NewGuid(),
            TransferId = transfer.Id,
            ApprovedBy = "operator-1"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.Forbidden);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public async Task ApproveTransfer_WhenManager_ShouldSetApprovedAndPublishEvent()
    {
        await using var db = CreateDbContext();
        var transfer = new Transfer
        {
            TransferNumber = "TRF-0001",
            FromWarehouse = "NLQ",
            ToWarehouse = "SCRAP",
            RequestedBy = "operator-1",
            RequestedAt = DateTimeOffset.UtcNow,
            CreateCommandId = Guid.NewGuid()
        };
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        var bus = new RecordingEventBus();
        var httpContextAccessor = new HttpContextAccessor
        {
            HttpContext = BuildHttpContextWithRoles(WarehouseRoles.WarehouseManager)
        };
        var handler = new ApproveTransferCommandHandler(
            db,
            bus,
            MockCurrentUser("manager-1"),
            httpContextAccessor);

        var result = await handler.Handle(new ApproveTransferCommand
        {
            CommandId = Guid.NewGuid(),
            TransferId = transfer.Id,
            ApprovedBy = "manager-1"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var persisted = await db.Transfers.SingleAsync();
        persisted.Status.Should().Be(TransferStatus.Approved);
        persisted.ApprovedBy.Should().Be("manager-1");
        bus.Published.OfType<TransferApprovedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public async Task ExecuteTransfer_WhenStockAvailable_ShouldCompleteAndEmitMoves()
    {
        await using var db = CreateDbContext();
        await SeedItemsAndLocationsAsync(db);

        var transfer = new Transfer
        {
            TransferNumber = "TRF-0002",
            FromWarehouse = "RES",
            ToWarehouse = "PROD",
            RequestedBy = "operator-1",
            RequestedAt = DateTimeOffset.UtcNow,
            CreateCommandId = Guid.NewGuid()
        };
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);
        transfer.Lines.Add(new TransferLine
        {
            ItemId = 1,
            Qty = 8m,
            FromLocationId = 11,
            ToLocationId = 12
        });
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        var availability = new Mock<ITransferStockAvailabilityService>(MockBehavior.Strict);
        availability
            .Setup(x => x.GetAvailableQtyAsync("LOC-RES-001", "ITEM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(20m);

        var bus = new RecordingEventBus();
        var stockLedger = new Mock<IStockLedgerRepository>(MockBehavior.Strict);
        stockLedger
            .Setup(x => x.LoadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StockLedger(), 0L));
        stockLedger
            .Setup(x => x.AppendEventAsync(
                It.IsAny<string>(),
                It.IsAny<StockMovedEvent>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ExecuteTransferCommandHandler(
            db,
            bus,
            availability.Object,
            stockLedger.Object,
            MockCurrentUser("operator-1"),
            Mock.Of<ILogger<ExecuteTransferCommandHandler>>());

        var result = await handler.Handle(new ExecuteTransferCommand
        {
            CommandId = Guid.NewGuid(),
            TransferId = transfer.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var persisted = await db.Transfers.Include(x => x.Lines).SingleAsync();
        persisted.Status.Should().Be(TransferStatus.Completed);
        db.Locations.Any(x => x.Code == "IN_TRANSIT_TRF-0002" && x.Type == "Virtual").Should().BeTrue();

        bus.Published.OfType<TransferExecutedEvent>().Should().ContainSingle();
        bus.Published.OfType<TransferCompletedEvent>().Should().ContainSingle();
        bus.Published.OfType<StockMovedEvent>().Should().HaveCount(2);
        stockLedger.Verify(
            x => x.AppendEventAsync(
                It.IsAny<string>(),
                It.IsAny<StockMovedEvent>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public async Task ExecuteTransfer_WhenInsufficientStock_ShouldReturnValidationError()
    {
        await using var db = CreateDbContext();
        await SeedItemsAndLocationsAsync(db);

        var transfer = new Transfer
        {
            TransferNumber = "TRF-0003",
            FromWarehouse = "NLQ",
            ToWarehouse = "SCRAP",
            RequestedBy = "operator-1",
            RequestedAt = DateTimeOffset.UtcNow,
            CreateCommandId = Guid.NewGuid()
        };
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);
        transfer.Approve("manager-1", Guid.NewGuid(), DateTimeOffset.UtcNow);
        transfer.Lines.Add(new TransferLine
        {
            ItemId = 1,
            Qty = 15m,
            FromLocationId = 11,
            ToLocationId = 12
        });
        db.Transfers.Add(transfer);
        await db.SaveChangesAsync();

        var availability = new Mock<ITransferStockAvailabilityService>(MockBehavior.Strict);
        availability
            .Setup(x => x.GetAvailableQtyAsync("LOC-RES-001", "ITEM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(5m);

        var bus = new RecordingEventBus();
        var stockLedger = new Mock<IStockLedgerRepository>(MockBehavior.Strict);
        var handler = new ExecuteTransferCommandHandler(
            db,
            bus,
            availability.Object,
            stockLedger.Object,
            MockCurrentUser("operator-1"),
            Mock.Of<ILogger<ExecuteTransferCommandHandler>>());

        var result = await handler.Handle(new ExecuteTransferCommand
        {
            CommandId = Guid.NewGuid(),
            TransferId = transfer.Id
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.InsufficientAvailableStock);
        var persisted = await db.Transfers.SingleAsync();
        persisted.Status.Should().Be(TransferStatus.Approved);
        bus.Published.Should().BeEmpty();
        stockLedger.Verify(
            x => x.AppendEventAsync(
                It.IsAny<string>(),
                It.IsAny<StockMovedEvent>(),
                It.IsAny<long>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"transfers-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }

    private static async Task SeedItemsAndLocationsAsync(WarehouseDbContext db)
    {
        db.Items.Add(new Item
        {
            Id = 1,
            InternalSKU = "ITEM-001",
            Name = "Item 1",
            CategoryId = 1,
            BaseUoM = "PCS",
            Status = "Active"
        });

        db.Locations.AddRange(
            new Location
            {
                Id = 11,
                Code = "LOC-RES-001",
                Barcode = "BAR-LOC-RES-001",
                Type = "Bin",
                Status = "Active"
            },
            new Location
            {
                Id = 12,
                Code = "LOC-PROD-001",
                Barcode = "BAR-LOC-PROD-001",
                Type = "Bin",
                Status = "Active"
            });

        await db.SaveChangesAsync();
    }

    private static ICurrentUserService MockCurrentUser(string userId)
    {
        var mock = new Mock<ICurrentUserService>(MockBehavior.Strict);
        mock.Setup(x => x.GetCurrentUserId()).Returns(userId);
        return mock.Object;
    }

    private static HttpContext BuildHttpContextWithRoles(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "user-1"),
            new(ClaimTypes.Name, "user-1")
        };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
    }

    private sealed class RecordingEventBus : IEventBus
    {
        public List<object> Published { get; } = new();

        public Task PublishAsync<T>(T message, CancellationToken ct = default) where T : class
        {
            Published.Add(message);
            return Task.CompletedTask;
        }
    }
}
