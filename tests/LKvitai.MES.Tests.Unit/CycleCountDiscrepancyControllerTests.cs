using System.Security.Claims;
using FluentAssertions;
using LKvitai.MES.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class CycleCountDiscrepancyControllerTests
{
    [Fact]
    public async Task GetDiscrepanciesAsync_ShouldReturnLinesAboveThreshold()
    {
        await using var db = CreateDbContext();
        var cycleCount = await SeedCycleCountAsync(db, delta: -5m, systemQty: 50m, physicalQty: 45m, unitCost: 2m);
        await AddNonDiscrepancyLineAsync(db, cycleCount.Id);

        var controller = CreateController(db, roles: []);

        var response = await controller.GetDiscrepanciesAsync(cycleCount.Id);

        response.Should().BeOfType<OkObjectResult>();
        var rows = ((OkObjectResult)response).Value.Should()
            .BeAssignableTo<IReadOnlyList<CycleCountsController.DiscrepancyLineResponse>>().Subject;
        rows.Should().HaveCount(1);
        rows[0].Variance.Should().Be(-5m);
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_WhenValueImpactAboveThresholdWithoutCfo_ShouldFail()
    {
        await using var db = CreateDbContext();
        var cycleCount = await SeedCycleCountAsync(db, delta: -100m, systemQty: 100m, physicalQty: 0m, unitCost: 20m);
        var lineId = cycleCount.Lines.Single().Id;

        var controller = CreateController(db, roles: []);
        var response = await controller.ApproveAdjustmentAsync(
            cycleCount.Id,
            new CycleCountsController.ApproveAdjustmentRequest(
                Guid.NewGuid(),
                [lineId],
                "manager",
                "Cycle count correction"));

        response.Should().BeOfType<ObjectResult>();
        ((ObjectResult)response).StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public async Task ApproveAdjustmentAsync_WhenApproved_ShouldPublishEventAndPersistApproval()
    {
        await using var db = CreateDbContext();
        var cycleCount = await SeedCycleCountAsync(db, delta: -5m, systemQty: 50m, physicalQty: 45m, unitCost: 10m);
        var lineId = cycleCount.Lines.Single().Id;
        var bus = new RecordingEventBus();

        var controller = CreateController(db, bus, roles: []);
        var response = await controller.ApproveAdjustmentAsync(
            cycleCount.Id,
            new CycleCountsController.ApproveAdjustmentRequest(
                Guid.NewGuid(),
                [lineId],
                "manager",
                "Cycle count correction"));

        response.Should().BeOfType<OkObjectResult>();
        var persisted = await db.CycleCountLines.SingleAsync(x => x.Id == lineId);
        persisted.AdjustmentApprovedBy.Should().Be("manager");
        persisted.AdjustmentApprovedAt.Should().NotBeNull();
        persisted.Status.Should().Be(CycleCountLineStatus.Approved);
        bus.Published.OfType<StockAdjustedEvent>().Should().ContainSingle();
    }

    private static WarehouseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase($"cycle-discrepancy-tests-{Guid.NewGuid():N}")
            .Options;
        return new WarehouseDbContext(options);
    }

    private static CycleCountsController CreateController(
        WarehouseDbContext db,
        RecordingEventBus? bus = null,
        string[]? roles = null)
    {
        var currentUser = new Mock<ICurrentUserService>(MockBehavior.Strict);
        currentUser.Setup(x => x.GetCurrentUserId()).Returns("user-1");

        var httpContext = BuildHttpContextWithRoles(roles ?? []);
        var accessor = new HttpContextAccessor { HttpContext = httpContext };

        return new CycleCountsController(
            Mock.Of<IMediator>(),
            db,
            bus ?? new RecordingEventBus(),
            currentUser.Object,
            accessor)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static async Task<CycleCount> SeedCycleCountAsync(
        WarehouseDbContext db,
        decimal delta,
        decimal systemQty,
        decimal physicalQty,
        decimal unitCost)
    {
        db.Items.Add(new Item
        {
            Id = 1,
            InternalSKU = "ITEM-001",
            Name = "Item 1",
            BaseUoM = "PCS",
            Status = "Active",
            CategoryId = 1
        });
        db.Locations.Add(new Location
        {
            Id = 11,
            Code = "LOC-001",
            Barcode = "LOC-001",
            Type = "Bin",
            Status = "Active"
        });
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

        var cycleCount = new CycleCount
        {
            Id = Guid.NewGuid(),
            CountNumber = "CC-20260212-001",
            ScheduledDate = DateTimeOffset.UtcNow,
            AbcClass = "ALL",
            AssignedOperator = "operator-1",
            ScheduleCommandId = Guid.NewGuid()
        };
        cycleCount.Lines.Add(new CycleCountLine
        {
            Id = Guid.NewGuid(),
            LocationId = 11,
            ItemId = 1,
            SystemQty = systemQty,
            PhysicalQty = physicalQty,
            Delta = delta,
            CountedAt = DateTimeOffset.UtcNow,
            CountedBy = "operator-1",
            Status = CycleCountLineStatus.Pending
        });

        db.CycleCounts.Add(cycleCount);
        await db.SaveChangesAsync();
        return cycleCount;
    }

    private static async Task AddNonDiscrepancyLineAsync(WarehouseDbContext db, Guid cycleCountId)
    {
        db.CycleCountLines.Add(new CycleCountLine
        {
            Id = Guid.NewGuid(),
            CycleCountId = cycleCountId,
            LocationId = 11,
            ItemId = 1,
            SystemQty = 100m,
            PhysicalQty = 99m,
            Delta = -1m,
            CountedAt = DateTimeOffset.UtcNow,
            CountedBy = "operator-1",
            Status = CycleCountLineStatus.Pending
        });
        await db.SaveChangesAsync();
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
