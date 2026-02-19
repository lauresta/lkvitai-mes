using FluentAssertions;
using LKvitai.MES.Application.Commands;
using LKvitai.MES.Application.Orchestration;
using LKvitai.MES.SharedKernel;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

/// <summary>
/// Unit tests for AllocateReservationCommandHandler.
/// </summary>
public class AllocateReservationCommandHandlerTests
{
    private readonly Mock<IAllocateReservationOrchestration> _orchestrationMock = new();
    private readonly Mock<ILogger<AllocateReservationCommandHandler>> _loggerMock = new();
    private AllocateReservationCommandHandler CreateHandler() =>
        new(_orchestrationMock.Object, _loggerMock.Object);

    [Fact]
    public async Task Handle_WhenOrchestrationSucceeds_ReturnsOk()
    {
        // Arrange
        _orchestrationMock
            .Setup(x => x.AllocateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var handler = CreateHandler();
        var command = new AllocateReservationCommand
        {
            ReservationId = Guid.NewGuid(),
            WarehouseId = "WH1"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_WhenOrchestrationFails_ReturnsFailure()
    {
        // Arrange
        _orchestrationMock
            .Setup(x => x.AllocateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(DomainErrorCodes.InsufficientAvailableStock));

        var handler = CreateHandler();
        var command = new AllocateReservationCommand
        {
            ReservationId = Guid.NewGuid(),
            WarehouseId = "WH1"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrorCodes.InsufficientAvailableStock);
    }

    [Fact]
    public async Task Handle_DelegatesCorrectParameters()
    {
        // Arrange
        var reservationId = Guid.NewGuid();
        var warehouseId = "WH1";

        _orchestrationMock
            .Setup(x => x.AllocateAsync(reservationId, warehouseId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Ok());

        var handler = CreateHandler();
        var command = new AllocateReservationCommand
        {
            ReservationId = reservationId,
            WarehouseId = warehouseId
        };

        // Act
        await handler.Handle(command, CancellationToken.None);

        // Assert
        _orchestrationMock.Verify(x => x.AllocateAsync(
            reservationId, warehouseId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ReservationNotFound_ReturnsCorrectError()
    {
        // Arrange
        _orchestrationMock
            .Setup(x => x.AllocateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Fail(DomainErrorCodes.ReservationNotFound));

        var handler = CreateHandler();
        var command = new AllocateReservationCommand
        {
            ReservationId = Guid.NewGuid(),
            WarehouseId = "WH1"
        };

        // Act
        var result = await handler.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrorCodes.ReservationNotFound);
    }
}
