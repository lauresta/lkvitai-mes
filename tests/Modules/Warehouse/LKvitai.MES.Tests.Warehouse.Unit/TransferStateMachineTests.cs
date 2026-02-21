using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class TransferStateMachineTests
{
    [Fact]
    [Trait("Category", "Transfers")]
    public void EnsureRequestedState_ShouldSetDraft()
    {
        var transfer = CreateTransfer("RES", "PROD");

        transfer.EnsureRequestedState();

        transfer.Status.Should().Be(TransferStatus.Draft);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void Submit_WhenNonScrap_ShouldSetApproved()
    {
        var transfer = CreateTransfer("RES", "PROD");
        transfer.EnsureRequestedState();

        var result = transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        transfer.Status.Should().Be(TransferStatus.Approved);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void Submit_WhenScrap_ShouldSetPendingApproval()
    {
        var transfer = CreateTransfer("NLQ", "SCRAP");
        transfer.EnsureRequestedState();

        var result = transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        transfer.Status.Should().Be(TransferStatus.PendingApproval);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void Submit_WhenNotDraft_ShouldFail()
    {
        var transfer = CreateTransfer("RES", "PROD");
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void Approve_WhenPendingApproval_ShouldSetApproved()
    {
        var transfer = CreateTransfer("NLQ", "SCRAP");
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = transfer.Approve("manager-1", Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        transfer.Status.Should().Be(TransferStatus.Approved);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void Approve_WhenNotPendingApproval_ShouldFail()
    {
        var transfer = CreateTransfer("RES", "PROD");
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = transfer.Approve("manager-1", Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void StartExecution_WhenApproved_ShouldSetInTransit()
    {
        var transfer = CreateTransfer("RES", "PROD");
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = transfer.StartExecution("operator-1", Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        transfer.Status.Should().Be(TransferStatus.InTransit);
        transfer.ExecutedBy.Should().Be("operator-1");
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void StartExecution_WhenNotApproved_ShouldFail()
    {
        var transfer = CreateTransfer("RES", "PROD");
        transfer.EnsureRequestedState();

        var result = transfer.StartExecution("operator-1", Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void StartExecution_WhenExecutedByMissing_ShouldFail()
    {
        var transfer = CreateTransfer("RES", "PROD");
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = transfer.StartExecution(string.Empty, Guid.NewGuid(), DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void Complete_WhenInTransit_ShouldSetCompleted()
    {
        var transfer = CreateTransfer("RES", "PROD");
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);
        transfer.StartExecution("operator-1", Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = transfer.Complete(DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeTrue();
        transfer.Status.Should().Be(TransferStatus.Completed);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void Complete_WhenNotInTransit_ShouldFail()
    {
        var transfer = CreateTransfer("RES", "PROD");
        transfer.EnsureRequestedState();
        transfer.Submit(Guid.NewGuid(), DateTimeOffset.UtcNow);

        var result = transfer.Complete(DateTimeOffset.UtcNow);

        result.IsSuccess.Should().BeFalse();
        result.ErrorCode.Should().Be(DomainErrorCodes.ValidationError);
    }

    [Fact]
    [Trait("Category", "Transfers")]
    public void RequiresApproval_ShouldOnlyRequireForScrap()
    {
        var scrapTransfer = CreateTransfer("NLQ", "SCRAP");
        var prodTransfer = CreateTransfer("RES", "PROD");

        scrapTransfer.RequiresApproval().Should().BeTrue();
        prodTransfer.RequiresApproval().Should().BeFalse();
    }

    private static Transfer CreateTransfer(string fromWarehouse, string toWarehouse)
    {
        return new Transfer
        {
            TransferNumber = "TRF-TEST",
            FromWarehouse = fromWarehouse,
            ToWarehouse = toWarehouse,
            RequestedBy = "operator-1",
            RequestedAt = DateTimeOffset.UtcNow,
            CreateCommandId = Guid.NewGuid()
        };
    }
}
