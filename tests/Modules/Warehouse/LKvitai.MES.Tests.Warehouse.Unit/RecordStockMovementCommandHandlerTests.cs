using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>
/// Tests for RecordStockMovementCommandHandler — verifies expected-version append (V-2),
/// bounded retry (max 3), domain error propagation, and correct stream ID routing per ADR-001.
/// </summary>
public class RecordStockMovementCommandHandlerTests
{
    private readonly Mock<IStockLedgerRepository> _repoMock = new();
    private readonly Mock<IBalanceGuardLockFactory> _lockFactoryMock = new();
    private readonly Mock<IBalanceGuardLock> _lockMock = new();
    private readonly Mock<ILogger<RecordStockMovementCommandHandler>> _loggerMock = new();
    private readonly RecordStockMovementCommandHandler _handler;

    public RecordStockMovementCommandHandlerTests()
    {
        // [HOTFIX CRIT-01] Set up mock lock factory to return a mock lock
        _lockMock.Setup(l => l.AcquireAsync(It.IsAny<long[]>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _lockMock.Setup(l => l.CommitAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _lockMock.Setup(l => l.DisposeAsync())
            .Returns(ValueTask.CompletedTask);
        _lockFactoryMock.Setup(f => f.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(_lockMock.Object);

        _handler = new RecordStockMovementCommandHandler(
            _repoMock.Object, _lockFactoryMock.Object, _loggerMock.Object);
    }

    // ── Happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Receipt_ShouldSucceed_OnFirstAttempt()
    {
        // Arrange: receipt → stream is (WH1, LOC-1, SKU-A)
        var streamId = StockLedgerStreamId.For("WH1", "LOC-1", "SKU-A");
        _repoMock.Setup(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StockLedger(), 0L));
        _repoMock.Setup(r => r.AppendEventAsync(streamId, It.IsAny<StockMovedEvent>(), 0L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = MakeReceiptCommand("WH1", "LOC-1", "SKU-A", 10m);

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repoMock.Verify(r => r.AppendEventAsync(streamId, It.IsAny<StockMovedEvent>(), 0L, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Dispatch_ShouldRouteToFromLocationStream()
    {
        // Arrange: dispatch from LOC-X → stream is (WH1, LOC-X, SKU-B)
        var streamId = StockLedgerStreamId.For("WH1", "LOC-X", "SKU-B");
        var ledger = new StockLedger();
        ledger.Apply(MakeReceiptEvent("LOC-X", "SKU-B", 100m));

        _repoMock.Setup(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ledger, 1L));
        _repoMock.Setup(r => r.AppendEventAsync(streamId, It.IsAny<StockMovedEvent>(), 1L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new RecordStockMovementCommand
        {
            WarehouseId = "WH1",
            SKU = "SKU-B",
            Quantity = 30m,
            FromLocation = "LOC-X",
            ToLocation = "",
            MovementType = MovementType.Dispatch,
            OperatorId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repoMock.Verify(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_Transfer_ShouldRouteToFromLocationStream()
    {
        // Arrange: transfer from LOC-A to LOC-B → stream is (WH1, LOC-A, SKU-A)
        var streamId = StockLedgerStreamId.For("WH1", "LOC-A", "SKU-A");
        var ledger = new StockLedger();
        ledger.Apply(MakeReceiptEvent("LOC-A", "SKU-A", 50m));

        _repoMock.Setup(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ledger, 1L));
        _repoMock.Setup(r => r.AppendEventAsync(streamId, It.IsAny<StockMovedEvent>(), 1L, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new RecordStockMovementCommand
        {
            WarehouseId = "WH1",
            SKU = "SKU-A",
            Quantity = 20m,
            FromLocation = "LOC-A",
            ToLocation = "LOC-B",
            MovementType = MovementType.Transfer,
            OperatorId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repoMock.Verify(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── Concurrency retry: succeeds on 2nd attempt ───────────────────────

    [Fact]
    public async Task Handle_ShouldRetry_OnConcurrencyConflict_AndSucceedOnSecondAttempt()
    {
        var streamId = StockLedgerStreamId.For("WH1", "LOC-1", "SKU-A");
        int loadCallCount = 0;

        _repoMock.Setup(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                loadCallCount++;
                return (new StockLedger(), (long)(loadCallCount - 1));
            });

        int appendCallCount = 0;
        _repoMock.Setup(r => r.AppendEventAsync(streamId, It.IsAny<StockMovedEvent>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                appendCallCount++;
                if (appendCallCount == 1)
                    throw new ConcurrencyException("Version conflict");
                return Task.CompletedTask;
            });

        var cmd = MakeReceiptCommand("WH1", "LOC-1", "SKU-A", 5m);

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _repoMock.Verify(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()), Times.Exactly(2));
        _repoMock.Verify(r => r.AppendEventAsync(streamId, It.IsAny<StockMovedEvent>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    // ── Bounded retry: fails after MaxRetries ────────────────────────────

    [Fact]
    public async Task Handle_ShouldFail_AfterMaxRetries()
    {
        var streamId = StockLedgerStreamId.For("WH1", "LOC-1", "SKU-A");

        _repoMock.Setup(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StockLedger(), 0L));
        _repoMock.Setup(r => r.AppendEventAsync(streamId, It.IsAny<StockMovedEvent>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ConcurrencyException("Always conflicts"));

        var cmd = MakeReceiptCommand("WH1", "LOC-1", "SKU-A", 5m);

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Concurrency conflict after");
        _repoMock.Verify(
            r => r.AppendEventAsync(streamId, It.IsAny<StockMovedEvent>(), It.IsAny<long>(), It.IsAny<CancellationToken>()),
            Times.Exactly(RecordStockMovementCommandHandler.MaxRetries));
    }

    // ── Domain error: no retry ───────────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldFailImmediately_OnDomainError()
    {
        // Arrange: empty ledger → dispatch from empty location will fail
        var streamId = StockLedgerStreamId.For("WH1", "LOC-X", "SKU-A");
        _repoMock.Setup(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StockLedger(), 0L));

        var cmd = new RecordStockMovementCommand
        {
            WarehouseId = "WH1",
            SKU = "SKU-A",
            Quantity = 10m,
            FromLocation = "LOC-X",
            ToLocation = "",
            MovementType = MovementType.Dispatch,
            OperatorId = Guid.NewGuid()
        };

        // Act
        var result = await _handler.Handle(cmd, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("Insufficient balance");
        // Should NOT retry
        _repoMock.Verify(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()), Times.Once);
        _repoMock.Verify(r => r.AppendEventAsync(It.IsAny<string>(), It.IsAny<StockMovedEvent>(), It.IsAny<long>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── Domain error: zero quantity ──────────────────────────────────────

    [Fact]
    public async Task Handle_ShouldFailImmediately_OnZeroQuantity()
    {
        var streamId = StockLedgerStreamId.For("WH1", "LOC-1", "SKU-A");
        _repoMock.Setup(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((new StockLedger(), 0L));

        var cmd = new RecordStockMovementCommand
        {
            WarehouseId = "WH1",
            SKU = "SKU-A",
            Quantity = 0m,
            FromLocation = "",
            ToLocation = "LOC-1",
            MovementType = MovementType.Receipt,
            OperatorId = Guid.NewGuid()
        };

        var result = await _handler.Handle(cmd, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Contain("greater than zero");
    }

    // ── Integration-style: concurrent updates resolved by retry ──────────

    [Fact]
    public async Task Handle_ShouldReloadFreshState_OnRetry()
    {
        // Simulate: first load returns version 0, first append fails.
        // Second load returns version 1 (someone else appended), second append succeeds.
        var streamId = StockLedgerStreamId.For("WH1", "LOC-2", "SKU-B");
        var ledgerV0 = new StockLedger();
        var ledgerV1 = new StockLedger();
        ledgerV1.Apply(new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = "SKU-B",
            Quantity = 100m,
            FromLocation = "",
            ToLocation = "LOC-2",
            MovementType = MovementType.Receipt,
            OperatorId = Guid.NewGuid()
        });

        int loadCount = 0;
        _repoMock.Setup(r => r.LoadAsync(streamId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() =>
            {
                loadCount++;
                return loadCount == 1 ? (ledgerV0, 0L) : (ledgerV1, 1L);
            });

        int appendCount = 0;
        _repoMock.Setup(r => r.AppendEventAsync(streamId, It.IsAny<StockMovedEvent>(), It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                appendCount++;
                if (appendCount == 1)
                    throw new ConcurrencyException("Version conflict");
                return Task.CompletedTask;
            });

        // Receipt command — domain always passes.
        var receiptCmd = MakeReceiptCommand("WH1", "LOC-2", "SKU-B", 20m);

        // Act
        var result = await _handler.Handle(receiptCmd, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        loadCount.Should().Be(2, "handler should reload on retry");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private static RecordStockMovementCommand MakeReceiptCommand(
        string warehouseId, string toLocation, string sku, decimal qty)
        => new()
        {
            WarehouseId = warehouseId,
            SKU = sku,
            Quantity = qty,
            FromLocation = "",
            ToLocation = toLocation,
            MovementType = MovementType.Receipt,
            OperatorId = Guid.NewGuid()
        };

    private static StockMovedEvent MakeReceiptEvent(string toLocation, string sku, decimal qty)
        => new()
        {
            MovementId = Guid.NewGuid(),
            SKU = sku,
            Quantity = qty,
            FromLocation = "",
            ToLocation = toLocation,
            MovementType = MovementType.Receipt,
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        };
}
