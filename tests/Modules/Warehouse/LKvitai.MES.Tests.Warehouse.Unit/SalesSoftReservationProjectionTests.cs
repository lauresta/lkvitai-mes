using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Contracts.ReadModels;
using LKvitai.MES.Modules.Warehouse.Projections;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class SalesSoftReservationProjectionTests
{
    [Fact]
    public void SalesReservationCannotEnterProductionHardLockPath()
    {
        var reservation = new LKvitai.MES.Modules.Warehouse.Domain.Aggregates.Reservation();
        reservation.Apply(new ReservationCreatedEvent { Purpose = "SALES_ORDER" });
        var action = () => reservation.ValidateCanStartPicking();
        action.Should().Throw<LKvitai.MES.BuildingBlocks.SharedKernel.DomainException>();
    }

    [Theory]
    [InlineData(false, 10)]
    [InlineData(true, 5)]
    public void OnlySalesSoftAllocationReducesAvailable(bool sales, decimal available)
    {
        var view = new AvailableStockView { Id = "WH1:A:SKU", OnHandQty = 10 };
        var projection = new AvailableStockProjection();
        projection.Apply(new StockAllocatedEvent { SalesOrderReservation = sales,
            Allocations = [new() { WarehouseId = "WH1", Location = "A", SKU = "SKU", Quantity = 5 }] }, view);
        view.AvailableQty.Should().Be(available);
        view.OnHandQty.Should().Be(10);
        view.HardLockedQty.Should().Be(0);
    }

    [Fact]
    public void PartialPickAndCancellationReleaseOnlyTheirOwnSoftQuantities()
    {
        var projection = new AvailableStockProjection();
        var view = new AvailableStockView { Id = "WH1:A:SKU", OnHandQty = 10, SalesReservedQty = 8, HardLockedQty = 1 };
        projection.Apply(new ReservationConsumedEvent { Completed = false,
            ReleasedSoftLines = [new() { WarehouseId = "WH1", Location = "A", SKU = "SKU", Quantity = 2 }] }, view);
        view.SalesReservedQty.Should().Be(6);
        view.HardLockedQty.Should().Be(1);
        projection.Apply(new ReservationCancelledEvent {
            ReleasedSoftLines = [new() { WarehouseId = "WH1", Location = "A", SKU = "SKU", Quantity = 3 }] }, view);
        view.SalesReservedQty.Should().Be(3);
        view.AvailableQty.Should().Be(6);
        var summary = new ReservationSummaryView { Status = "ALLOCATED" };
        ReservationSummaryAggregation.ApplyConsumed(new ReservationConsumedEvent { Completed = false }, summary);
        summary.Status.Should().Be("ALLOCATED");
    }
}
