using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Application.Behaviors;
using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.SharedKernel;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>
/// Unit tests for <see cref="IdempotencyBehavior{TRequest, TResponse}"/>.
/// Uses mock <see cref="IProcessedCommandStore"/> to verify behavior logic in isolation.
/// </summary>
[Trait("Category", "Idempotency")]
public class IdempotencyBehaviorTests
{
    private readonly Mock<IProcessedCommandStore> _store;
    private readonly IdempotencyBehavior<TestIdempotencyCommand, Result> _behavior;

    public IdempotencyBehaviorTests()
    {
        _store = new Mock<IProcessedCommandStore>();
        var logger = new Mock<ILogger<IdempotencyBehavior<TestIdempotencyCommand, Result>>>();
        _behavior = new IdempotencyBehavior<TestIdempotencyCommand, Result>(_store.Object, logger.Object);
    }

    // ── TryStartAsync → Started: handler executes, CompleteAsync called ──

    [Fact]
    public async Task Started_ExecutesHandler_AndCallsComplete()
    {
        _store
            .Setup(s => s.TryStartAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandClaimResult.Started);

        var handlerCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            handlerCalled = true;
            return Task.FromResult(Result.Ok());
        };

        var result = await _behavior.Handle(
            new TestIdempotencyCommand(), next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handlerCalled.Should().BeTrue("Handler should have been invoked");
        _store.Verify(
            s => s.CompleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Once);
        _store.Verify(
            s => s.FailAsync(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TryStartAsync → Started + handler fails: FailAsync called ──

    [Fact]
    public async Task Started_HandlerReturnsFailure_CallsFail()
    {
        _store
            .Setup(s => s.TryStartAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandClaimResult.Started);

        RequestHandlerDelegate<Result> next = () =>
            Task.FromResult(Result.Fail("some_error"));

        var result = await _behavior.Handle(
            new TestIdempotencyCommand(), next, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be("some_error");
        _store.Verify(
            s => s.FailAsync(It.IsAny<Guid>(), "some_error", It.IsAny<CancellationToken>()),
            Times.Once);
        _store.Verify(
            s => s.CompleteAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // ── TryStartAsync → Started + handler throws: FailAsync called, rethrown ──

    [Fact]
    public async Task Started_HandlerThrows_CallsFail_AndRethrows()
    {
        _store
            .Setup(s => s.TryStartAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandClaimResult.Started);

        RequestHandlerDelegate<Result> next = () =>
            throw new InvalidOperationException("boom");

        var act = () => _behavior.Handle(
            new TestIdempotencyCommand(), next, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("boom");
        _store.Verify(
            s => s.FailAsync(It.IsAny<Guid>(), nameof(InvalidOperationException), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── TryStartAsync → AlreadyCompleted: handler NOT invoked, returns OK ──

    [Fact]
    public async Task AlreadyCompleted_ShortCircuitsOk_HandlerNotInvoked()
    {
        _store
            .Setup(s => s.TryStartAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandClaimResult.AlreadyCompleted);

        var handlerCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            handlerCalled = true;
            return Task.FromResult(Result.Ok());
        };

        var result = await _behavior.Handle(
            new TestIdempotencyCommand(), next, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        handlerCalled.Should().BeFalse("Handler must NOT run for already-completed commands");
    }

    // ── TryStartAsync → InProgress: handler NOT invoked, returns error ──

    [Fact]
    public async Task InProgress_ReturnsFail_HandlerNotInvoked()
    {
        _store
            .Setup(s => s.TryStartAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandClaimResult.InProgress);

        var handlerCalled = false;
        RequestHandlerDelegate<Result> next = () =>
        {
            handlerCalled = true;
            return Task.FromResult(Result.Ok());
        };

        var result = await _behavior.Handle(
            new TestIdempotencyCommand(), next, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error.Should().Be(DomainErrorCodes.IdempotencyInProgress);
        handlerCalled.Should().BeFalse("Handler must NOT run when another instance holds the claim");
    }

    // ── TryStartAsync passes correct CommandId and CommandType ──

    [Fact]
    public async Task TryStart_ReceivesCorrectCommandIdAndType()
    {
        var commandId = Guid.NewGuid();
        _store
            .Setup(s => s.TryStartAsync(commandId, nameof(TestIdempotencyCommand), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CommandClaimResult.AlreadyCompleted)
            .Verifiable();

        await _behavior.Handle(
            new TestIdempotencyCommand { CommandId = commandId },
            () => Task.FromResult(Result.Ok()),
            CancellationToken.None);

        _store.Verify();
    }

    // ── Test command ────────────────────────────────────────────────────

    public record TestIdempotencyCommand : ICommand
    {
        public Guid CommandId { get; init; } = Guid.NewGuid();
        public Guid CorrelationId { get; init; }
        public Guid CausationId { get; init; }
    }
}
