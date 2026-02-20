using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class CycleCountCommandHandlersTests
{
    [Fact]
    [Trait("Category", "CycleCounting")]
    public async Task ScheduleCycleCount_ShouldCreateScheduledCycleCount()
    {
        await using var db = CreateDbContext();
        await SeedItemsAndLocationsAsync(db);

        var resolver = new Mock<ICycleCountQuantityResolver>(MockBehavior.Strict);
        resolver
            .Setup(x => x.ResolveSystemQtyAsync("LOC-001", "ITEM-001", It.IsAny<CancellationToken>()))
            .ReturnsAsync(25m);

        var bus = new RecordingEventBus();
        var handler = new ScheduleCycleCountCommandHandler(
            db,
            bus,
            resolver.Object,
            Mock.Of<ILogger<ScheduleCycleCountCommandHandler>>());

        var result = await handler.Handle(new ScheduleCycleCountCommand
        {
            CommandId = Guid.NewGuid(),
            ScheduledDate = DateTimeOffset.UtcNow.Date.AddDays(1),
            AbcClass = "A",
            AssignedOperator = "operator-1",
            LocationIds = [11]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var cycleCount = await db.CycleCounts.Include(x => x.Lines).SingleAsync();
        cycleCount.Status.Should().Be(CycleCountStatus.Scheduled);
        cycleCount.Lines.Should().ContainSingle();
        cycleCount.Lines.Single().SystemQty.Should().Be(25m);
        bus.Published.OfType<CycleCountScheduledEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "CycleCounting")]
    public async Task RecordCount_ShouldUpdateLineAndCompleteCycleCountWhenAllLinesCounted()
    {
        await using var db = CreateDbContext();
        var cycleCountId = await SeedScheduledCycleCountAsync(db, 100m, 0m);

        var bus = new RecordingEventBus();
        var handler = new RecordCountCommandHandler(
            db,
            bus,
            MockCurrentUser("operator-1"),
            Mock.Of<ILogger<RecordCountCommandHandler>>());

        var result = await handler.Handle(new RecordCountCommand
        {
            CommandId = Guid.NewGuid(),
            CycleCountId = cycleCountId,
            LocationId = 11,
            ItemId = 1,
            PhysicalQty = 95m,
            CountedBy = "operator-1"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var cycleCount = await db.CycleCounts.Include(x => x.Lines).SingleAsync();
        cycleCount.Status.Should().Be(CycleCountStatus.Completed);
        cycleCount.Lines.Single().PhysicalQty.Should().Be(95m);
        cycleCount.Lines.Single().Delta.Should().Be(-5m);
        cycleCount.Lines.Single().CountedBy.Should().Be("operator-1");
        cycleCount.Lines.Single().CountedAt.Should().NotBeNull();
        bus.Published.OfType<CountRecordedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "CycleCounting")]
    public async Task ApplyAdjustment_WhenNoManagerApprovalRequired_ShouldComplete()
    {
        await using var db = CreateDbContext();
        await SeedItemLocationAndCostAsync(db, unitCost: 1m);
        var cycleCountId = await SeedScheduledCycleCountAsync(db, 100m, 95m);

        var bus = new RecordingEventBus();
        var handler = new ApplyAdjustmentCommandHandler(
            db,
            bus,
            MockCurrentUser("operator-1"),
            new HttpContextAccessor { HttpContext = BuildHttpContextWithRoles() },
            Mock.Of<IBusinessTelemetryService>(),
            Mock.Of<ILogger<ApplyAdjustmentCommandHandler>>());

        var result = await handler.Handle(new ApplyAdjustmentCommand
        {
            CommandId = Guid.NewGuid(),
            CycleCountId = cycleCountId
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var cycleCount = await db.CycleCounts.Include(x => x.Lines).SingleAsync();
        cycleCount.Status.Should().Be(CycleCountStatus.Completed);
        cycleCount.Lines.Single().Status.Should().Be(CycleCountLineStatus.Approved);
        bus.Published.OfType<StockAdjustedEvent>().Should().ContainSingle();
        bus.Published.OfType<CycleCountCompletedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "CycleCounting")]
    public async Task ApplyAdjustment_WhenLargeDiscrepancyWithoutManager_ShouldFail()
    {
        await using var db = CreateDbContext();
        await SeedItemLocationAndCostAsync(db, unitCost: 10m);
        var cycleCountId = await SeedScheduledCycleCountAsync(db, 100m, 70m);

        var handler = new ApplyAdjustmentCommandHandler(
            db,
            new RecordingEventBus(),
            MockCurrentUser("operator-1"),
            new HttpContextAccessor { HttpContext = BuildHttpContextWithRoles() },
            Mock.Of<IBusinessTelemetryService>(),
            Mock.Of<ILogger<ApplyAdjustmentCommandHandler>>());

        var result = await handler.Handle(new ApplyAdjustmentCommand
        {
            CommandId = Guid.NewGuid(),
            CycleCountId = cycleCountId
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.Error.Should().Contain("Manager approval required");
    }

    [Fact]
    [Trait("Category", "CycleCounting")]
    public async Task ApplyAdjustment_WhenLargeDiscrepancyWithManager_ShouldPass()
    {
        await using var db = CreateDbContext();
        await SeedItemLocationAndCostAsync(db, unitCost: 10m);
        var cycleCountId = await SeedScheduledCycleCountAsync(db, 100m, 70m);

        var bus = new RecordingEventBus();
        var handler = new ApplyAdjustmentCommandHandler(
            db,
            bus,
            MockCurrentUser("manager-1"),
            new HttpContextAccessor
            {
                HttpContext = BuildHttpContextWithRoles(WarehouseRoles.WarehouseManager)
            },
            Mock.Of<IBusinessTelemetryService>(),
            Mock.Of<ILogger<ApplyAdjustmentCommandHandler>>());

        var result = await handler.Handle(new ApplyAdjustmentCommand
        {
            CommandId = Guid.NewGuid(),
            CycleCountId = cycleCountId,
            ApproverId = "manager-1"
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var cycleCount = await db.CycleCounts.Include(x => x.Lines).SingleAsync();
        cycleCount.Status.Should().Be(CycleCountStatus.Completed);
        cycleCount.ApprovedBy.Should().Be("manager-1");
        bus.Published.OfType<StockAdjustedEvent>().Should().ContainSingle();
    }

    [Fact]
    [Trait("Category", "CycleCounting")]
    public async Task ScheduleCycleCount_WhenNoActiveItems_ShouldFail()
    {
        await using var db = CreateDbContext();
        db.Locations.Add(new Location
        {
            Id = 11,
            Code = "LOC-001",
            Barcode = "BAR-LOC-001",
            Type = "Bin",
            Status = "Active"
        });
        await db.SaveChangesAsync();

        var handler = new ScheduleCycleCountCommandHandler(
            db,
            new RecordingEventBus(),
            Mock.Of<ICycleCountQuantityResolver>(),
            Mock.Of<ILogger<ScheduleCycleCountCommandHandler>>());

        var result = await handler.Handle(new ScheduleCycleCountCommand
        {
            CommandId = Guid.NewGuid(),
            ScheduledDate = DateTimeOffset.UtcNow,
            AbcClass = "ALL",
            AssignedOperator = "operator-1",
            LocationIds = [11]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "CycleCounting")]
    public async Task ScheduleCycleCount_WhenPastDate_ShouldFail()
    {
        await using var db = CreateDbContext();
        await SeedItemsAndLocationsAsync(db);

        var handler = new ScheduleCycleCountCommandHandler(
            db,
            new RecordingEventBus(),
            Mock.Of<ICycleCountQuantityResolver>(),
            Mock.Of<ILogger<ScheduleCycleCountCommandHandler>>());

        var result = await handler.Handle(new ScheduleCycleCountCommand
        {
            CommandId = Guid.NewGuid(),
            ScheduledDate = DateTimeOffset.UtcNow.Date.AddDays(-1),
            AbcClass = "ALL",
            AssignedOperator = "operator-1",
            LocationIds = [11]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.Error.Should().Contain("Scheduled date");
    }

    [Fact]
    [Trait("Category", "CycleCounting")]
    public async Task ScheduleCycleCount_WhenAssignedOperatorMissing_ShouldFail()
    {
        await using var db = CreateDbContext();
        await SeedItemsAndLocationsAsync(db);

        var handler = new ScheduleCycleCountCommandHandler(
            db,
            new RecordingEventBus(),
            Mock.Of<ICycleCountQuantityResolver>(),
            Mock.Of<ILogger<ScheduleCycleCountCommandHandler>>());

        var result = await handler.Handle(new ScheduleCycleCountCommand
        {
            CommandId = Guid.NewGuid(),
            ScheduledDate = DateTimeOffset.UtcNow.Date.AddDays(1),
            AbcClass = "ALL",
            AssignedOperator = " ",
            LocationIds = [11]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
        result.Error.Should().Contain("AssignedOperator");
    }

    [Fact]
    [Trait("Category", "CycleCounting")]
    public async Task ScheduleCycleCount_ShouldPersistAbcClassAndAssignedOperator()
    {
        await using var db = CreateDbContext();
        await SeedItemsAndLocationsAsync(db);

        var resolver = new Mock<ICycleCountQuantityResolver>(MockBehavior.Strict);
        resolver
            .Setup(x => x.ResolveSystemQtyAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(0m);

        var handler = new ScheduleCycleCountCommandHandler(
            db,
            new RecordingEventBus(),
            resolver.Object,
            Mock.Of<ILogger<ScheduleCycleCountCommandHandler>>());

        var result = await handler.Handle(new ScheduleCycleCountCommand
        {
            CommandId = Guid.NewGuid(),
            ScheduledDate = DateTimeOffset.UtcNow.Date.AddDays(1),
            AbcClass = "ALL",
            AssignedOperator = "operator-42",
            LocationIds = [11]
        }, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var cycleCount = await db.CycleCounts.SingleAsync();
        cycleCount.AbcClass.Should().Be("ALL");
        cycleCount.AssignedOperator.Should().Be("operator-42");
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"cycle-count-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }

    private static async Task SeedItemsAndLocationsAsync(WarehouseDbContext db)
    {
        db.ItemCategories.Add(new ItemCategory
        {
            Id = 1,
            Code = "A-FAST",
            Name = "A Items"
        });
        db.Items.Add(new Item
        {
            Id = 1,
            InternalSKU = "ITEM-001",
            Name = "Item 1",
            CategoryId = 1,
            BaseUoM = "PCS",
            Status = "Active"
        });
        db.Locations.Add(new Location
        {
            Id = 11,
            Code = "LOC-001",
            Barcode = "BAR-LOC-001",
            Type = "Bin",
            Status = "Active"
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedItemLocationAndCostAsync(WarehouseDbContext db, decimal unitCost)
    {
        await SeedItemsAndLocationsAsync(db);
        db.OnHandValues.Add(new OnHandValue
        {
            Id = Guid.NewGuid(),
            ItemId = 1,
            ItemSku = "ITEM-001",
            ItemName = "Item 1",
            Qty = 100m,
            UnitCost = unitCost,
            TotalValue = 100m * unitCost,
            LastUpdated = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    private static async Task<Guid> SeedScheduledCycleCountAsync(WarehouseDbContext db, decimal systemQty, decimal physicalQty)
    {
        if (!await db.Items.AnyAsync())
        {
            await SeedItemsAndLocationsAsync(db);
        }

        var cycleCount = new CycleCount
        {
            CountNumber = "CC-0001",
            ScheduledDate = DateTimeOffset.UtcNow,
            ScheduleCommandId = Guid.NewGuid()
        };
        cycleCount.Lines.Add(new CycleCountLine
        {
            ItemId = 1,
            LocationId = 11,
            SystemQty = systemQty,
            PhysicalQty = physicalQty,
            Delta = physicalQty - systemQty,
            Status = CycleCountLineStatus.Pending
        });
        db.CycleCounts.Add(cycleCount);
        await db.SaveChangesAsync();
        return cycleCount.Id;
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
