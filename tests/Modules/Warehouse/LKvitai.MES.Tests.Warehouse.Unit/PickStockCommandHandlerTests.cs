using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Orchestration;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Contracts.Messages;
using LKvitai.MES.SharedKernel;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>
/// Unit tests for PickStockCommandHandler.
/// Verifies V-3 compliance: StockMovement recorded FIRST, HU NOT waited on.
/// Verifies deferred consumption published to event bus when consumption fails.
/// </summary>
public class PickStockCommandHandlerTests
{
    private readonly Mock<IPickStockOrchestration> _orchestrationMock = new();
    private readonly Mock<IEventBus> _eventBusMock = new();
    private readonly Mock<ILogger<PickStockCommandHandler>> _loggerMock = new();

    private PickStockCommandHandler CreateHandler() =>
        new(_orchestrationMock.Object, _eventBusMock.Object, _loggerMock.Object);

    private PickStockCommand CreateCommand() => new()
    {
        ReservationId = Guid.NewGuid(),
        HandlingUnitId = Guid.NewGuid(),
        WarehouseId = "WH1",
        SKU = "SKU-001",
        Quantity = 10m,
        FromLocation = "LOC-A",
        OperatorId = Guid.NewGuid(),
        CorrelationId = Guid.NewGuid()
    };

    [Fact]
    public async Task Handle_FullSuccess_ReturnsOk_NoEventBusPublish()
    {
        // Arrange
        var command = CreateCommand();
        var movementId = Guid.NewGuid();

        _orchestrationMock
            .Setup(x => x.ExecuteAsync(
                command.ReservationId, command.HandlingUnitId, command.WarehouseId,
                command.SKU, command.Quantity, command.FromLocation, command.OperatorId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PickStockResult.Ok(movementId));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<ConsumePickReservationDeferred>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_MovementFailed_ReturnsFailure_NoEventBusPublish()
    {
        // Arrange
        var command = CreateCommand();

        _orchestrationMock
            .Setup(x => x.ExecuteAsync(
                command.ReservationId, command.HandlingUnitId, command.WarehouseId,
                command.SKU, command.Quantity, command.FromLocation, command.OperatorId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PickStockResult.MovementFailed(DomainErrorCodes.PickStockMovementFailed));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrorCodes.PickStockMovementFailed);
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<ConsumePickReservationDeferred>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_ConsumptionDeferred_ReturnsOk_PublishesDeferredMessage()
    {
        // Arrange — movement committed but consumption failed
        var command = CreateCommand();
        var movementId = Guid.NewGuid();

        _orchestrationMock
            .Setup(x => x.ExecuteAsync(
                command.ReservationId, command.HandlingUnitId, command.WarehouseId,
                command.SKU, command.Quantity, command.FromLocation, command.OperatorId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PickStockResult.ConsumptionDeferred(
                movementId, DomainErrorCodes.PickStockConsumptionFailed));

        ConsumePickReservationDeferred? published = null;
        _eventBusMock
            .Setup(x => x.PublishAsync(It.IsAny<ConsumePickReservationDeferred>(), It.IsAny<CancellationToken>()))
            .Callback<ConsumePickReservationDeferred, CancellationToken>((msg, _) => published = msg)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert — returns OK because movement IS committed
        result.IsSuccess.Should().BeTrue();

        // Assert — deferred message published with correct data
        _eventBusMock.Verify(
            x => x.PublishAsync(It.IsAny<ConsumePickReservationDeferred>(), It.IsAny<CancellationToken>()),
            Times.Once);

        published.Should().NotBeNull();
        published!.ReservationId.Should().Be(command.ReservationId);
        published.MovementId.Should().Be(movementId);
        published.Quantity.Should().Be(command.Quantity);
        published.WarehouseId.Should().Be(command.WarehouseId);
        published.SKU.Should().Be(command.SKU);
        published.CorrelationId.Should().Be(command.CorrelationId);
    }

    [Fact]
    public async Task Handle_ReservationNotPicking_ReturnsError()
    {
        // Arrange
        var command = CreateCommand();

        _orchestrationMock
            .Setup(x => x.ExecuteAsync(
                command.ReservationId, command.HandlingUnitId, command.WarehouseId,
                command.SKU, command.Quantity, command.FromLocation, command.OperatorId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PickStockResult.MovementFailed(DomainErrorCodes.ReservationNotPicking));

        var handler = CreateHandler();

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrorCodes.ReservationNotPicking);
    }

    [Fact]
    public async Task Handle_ConsumptionDeferred_GeneratesCorrelationId_WhenEmpty()
    {
        // Arrange — command with empty CorrelationId
        var command = new PickStockCommand
        {
            ReservationId = Guid.NewGuid(),
            HandlingUnitId = Guid.NewGuid(),
            WarehouseId = "WH1",
            SKU = "SKU-001",
            Quantity = 5m,
            FromLocation = "LOC-B",
            OperatorId = Guid.NewGuid(),
            CorrelationId = Guid.Empty // empty!
        };

        var movementId = Guid.NewGuid();
        _orchestrationMock
            .Setup(x => x.ExecuteAsync(
                command.ReservationId, command.HandlingUnitId, command.WarehouseId,
                command.SKU, command.Quantity, command.FromLocation, command.OperatorId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(PickStockResult.ConsumptionDeferred(
                movementId, DomainErrorCodes.PickStockConsumptionFailed));

        ConsumePickReservationDeferred? published = null;
        _eventBusMock
            .Setup(x => x.PublishAsync(It.IsAny<ConsumePickReservationDeferred>(), It.IsAny<CancellationToken>()))
            .Callback<ConsumePickReservationDeferred, CancellationToken>((msg, _) => published = msg)
            .Returns(Task.CompletedTask);

        var handler = CreateHandler();

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert — a non-empty CorrelationId is generated
        published.Should().NotBeNull();
        published!.CorrelationId.Should().NotBe(Guid.Empty);
    }
}
