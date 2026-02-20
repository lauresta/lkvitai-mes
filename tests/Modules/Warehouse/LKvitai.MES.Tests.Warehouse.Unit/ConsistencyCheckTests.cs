using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.ConsistencyChecks;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.SharedKernel;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>
/// Unit tests for consistency check services.
/// </summary>
public class ConsistencyCheckTests
{
    // ── StuckReservationCheck ──────────────────────────────────────────

    [Fact]
    public async Task StuckReservation_WhenPickingBeyondTimeout_ReturnsAnomaly()
    {
        // Arrange
        var repoMock = new Mock<IReservationRepository>();
        var loggerMock = new Mock<ILogger<StuckReservationCheck>>();

        var stuckReservationId = Guid.NewGuid();
        var pickingStartedAt = DateTime.UtcNow - TimeSpan.FromHours(3); // 3 hours ago (> 2h threshold)

        repoMock.Setup(x => x.GetReservationsInStateAsync("PICKING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReservationStateDto>
            {
                new()
                {
                    ReservationId = stuckReservationId,
                    Status = "PICKING",
                    PickingStartedAt = pickingStartedAt
                }
            });

        var check = new StuckReservationCheck(repoMock.Object, loggerMock.Object);

        // Act
        var anomalies = await check.CheckAsync();

        // Assert
        anomalies.Should().HaveCount(1);
        anomalies[0].ErrorCode.Should().Be(DomainErrorCodes.StuckReservationDetected);
        anomalies[0].Metadata["ReservationId"].Should().Be(stuckReservationId);
    }

    [Fact]
    public async Task StuckReservation_WhenPickingWithinTimeout_ReturnsEmpty()
    {
        // Arrange
        var repoMock = new Mock<IReservationRepository>();
        var loggerMock = new Mock<ILogger<StuckReservationCheck>>();

        var recentReservationId = Guid.NewGuid();
        var pickingStartedAt = DateTime.UtcNow - TimeSpan.FromMinutes(30); // 30 min ago (< 2h threshold)

        repoMock.Setup(x => x.GetReservationsInStateAsync("PICKING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReservationStateDto>
            {
                new()
                {
                    ReservationId = recentReservationId,
                    Status = "PICKING",
                    PickingStartedAt = pickingStartedAt
                }
            });

        var check = new StuckReservationCheck(repoMock.Object, loggerMock.Object);

        // Act
        var anomalies = await check.CheckAsync();

        // Assert
        anomalies.Should().BeEmpty();
    }

    [Fact]
    public async Task StuckReservation_WhenNoPickingReservations_ReturnsEmpty()
    {
        // Arrange
        var repoMock = new Mock<IReservationRepository>();
        var loggerMock = new Mock<ILogger<StuckReservationCheck>>();

        repoMock.Setup(x => x.GetReservationsInStateAsync("PICKING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ReservationStateDto>());

        var check = new StuckReservationCheck(repoMock.Object, loggerMock.Object);

        // Act
        var anomalies = await check.CheckAsync();

        // Assert
        anomalies.Should().BeEmpty();
    }

    // ── OrphanHardLockCheck ────────────────────────────────────────────

    [Fact]
    public async Task OrphanHardLock_WhenReservationNotPicking_ReturnsAnomaly()
    {
        // Arrange
        var hardLocksMock = new Mock<IActiveHardLocksRepository>();
        var reservationMock = new Mock<IReservationRepository>();
        var loggerMock = new Mock<ILogger<OrphanHardLockCheck>>();

        var reservationId = Guid.NewGuid();

        hardLocksMock.Setup(x => x.GetAllActiveLocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActiveHardLockDto>
            {
                new()
                {
                    ReservationId = reservationId,
                    WarehouseId = "WH1",
                    Location = "LOC-A",
                    SKU = "SKU-001",
                    HardLockedQty = 10m
                }
            });

        // Reservation is CONSUMED (not PICKING) — hard lock is orphaned
        reservationMock.Setup(x => x.GetReservationStatusAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("CONSUMED");

        var check = new OrphanHardLockCheck(hardLocksMock.Object, reservationMock.Object, loggerMock.Object);

        // Act
        var anomalies = await check.CheckAsync();

        // Assert
        anomalies.Should().HaveCount(1);
        anomalies[0].ErrorCode.Should().Be(DomainErrorCodes.OrphanHardLockDetected);
        anomalies[0].Metadata["ReservationId"].Should().Be(reservationId);
        anomalies[0].Metadata["ReservationState"].Should().Be("CONSUMED");
    }

    [Fact]
    public async Task OrphanHardLock_WhenReservationNotFound_ReturnsAnomaly()
    {
        // Arrange
        var hardLocksMock = new Mock<IActiveHardLocksRepository>();
        var reservationMock = new Mock<IReservationRepository>();
        var loggerMock = new Mock<ILogger<OrphanHardLockCheck>>();

        var reservationId = Guid.NewGuid();

        hardLocksMock.Setup(x => x.GetAllActiveLocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActiveHardLockDto>
            {
                new()
                {
                    ReservationId = reservationId,
                    WarehouseId = "WH1",
                    Location = "LOC-B",
                    SKU = "SKU-002",
                    HardLockedQty = 5m
                }
            });

        // Reservation not found at all
        reservationMock.Setup(x => x.GetReservationStatusAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string?)null);

        var check = new OrphanHardLockCheck(hardLocksMock.Object, reservationMock.Object, loggerMock.Object);

        // Act
        var anomalies = await check.CheckAsync();

        // Assert
        anomalies.Should().HaveCount(1);
        anomalies[0].ErrorCode.Should().Be(DomainErrorCodes.OrphanHardLockDetected);
    }

    [Fact]
    public async Task OrphanHardLock_WhenReservationIsPicking_ReturnsEmpty()
    {
        // Arrange
        var hardLocksMock = new Mock<IActiveHardLocksRepository>();
        var reservationMock = new Mock<IReservationRepository>();
        var loggerMock = new Mock<ILogger<OrphanHardLockCheck>>();

        var reservationId = Guid.NewGuid();

        hardLocksMock.Setup(x => x.GetAllActiveLocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActiveHardLockDto>
            {
                new()
                {
                    ReservationId = reservationId,
                    WarehouseId = "WH1",
                    Location = "LOC-A",
                    SKU = "SKU-001",
                    HardLockedQty = 10m
                }
            });

        // Reservation IS in PICKING — not an orphan
        reservationMock.Setup(x => x.GetReservationStatusAsync(reservationId, It.IsAny<CancellationToken>()))
            .ReturnsAsync("PICKING");

        var check = new OrphanHardLockCheck(hardLocksMock.Object, reservationMock.Object, loggerMock.Object);

        // Act
        var anomalies = await check.CheckAsync();

        // Assert
        anomalies.Should().BeEmpty();
    }

    [Fact]
    public async Task OrphanHardLock_WhenNoActiveLocks_ReturnsEmpty()
    {
        // Arrange
        var hardLocksMock = new Mock<IActiveHardLocksRepository>();
        var reservationMock = new Mock<IReservationRepository>();
        var loggerMock = new Mock<ILogger<OrphanHardLockCheck>>();

        hardLocksMock.Setup(x => x.GetAllActiveLocksAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ActiveHardLockDto>());

        var check = new OrphanHardLockCheck(hardLocksMock.Object, reservationMock.Object, loggerMock.Object);

        // Act
        var anomalies = await check.CheckAsync();

        // Assert
        anomalies.Should().BeEmpty();
    }
}
