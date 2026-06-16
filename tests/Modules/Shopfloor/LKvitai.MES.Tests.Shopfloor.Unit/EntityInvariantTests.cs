using FluentAssertions;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;
using LKvitai.MES.Modules.Shopfloor.Domain;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit;

public class EntityInvariantTests
{
    [Fact]
    public void WorkCenter_TrimsAndRequiresCodeName()
    {
        var wc = new WorkCenter(Guid.NewGuid(), "  FAB  ", "  Fabrication  ");
        wc.Code.Should().Be("FAB");
        wc.Name.Should().Be("Fabrication");
    }

    [Theory]
    [InlineData("", "name")]
    [InlineData("code", "")]
    public void WorkCenter_BlankFields_Throw(string code, string name)
    {
        var act = () => new WorkCenter(Guid.NewGuid(), code, name);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkStation_RequiresWorkCenter()
    {
        var act = () => new WorkStation(Guid.NewGuid(), "ST1", "Station 1", Guid.Empty, null, true);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkStation_NegativeWipLimit_Throws()
    {
        var act = () => new WorkStation(Guid.NewGuid(), "ST1", "Station 1", Guid.NewGuid(), -1, true);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowTemplate_Create_IsDraft()
    {
        var wf = new WorkflowTemplate(Guid.NewGuid(), "ROLLER", "Roller", null, "{}", DateTimeOffset.UtcNow);
        wf.Status.Should().Be(WorkflowStatus.Draft);
        wf.Code.Should().Be("ROLLER");
    }

    [Fact]
    public void WorkflowTemplate_Create_BlankGraph_Throws()
    {
        var act = () => new WorkflowTemplate(Guid.NewGuid(), "ROLLER", "Roller", null, "  ", DateTimeOffset.UtcNow);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WorkflowTemplate_Publish_SetsPublishedAndUpdatedAt()
    {
        var wf = new WorkflowTemplate(Guid.NewGuid(), "ROLLER", "Roller", null, "{}", DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow.AddMinutes(5);

        wf.Publish(now);

        wf.Status.Should().Be(WorkflowStatus.Published);
        wf.UpdatedAt.Should().Be(now);
    }

    [Fact]
    public void WorkflowTemplate_SaveGraph_UpdatesJson()
    {
        var wf = new WorkflowTemplate(Guid.NewGuid(), "ROLLER", "Roller", null, "{}", DateTimeOffset.UtcNow);
        wf.SaveGraph("{\"nodes\":[]}", DateTimeOffset.UtcNow);
        wf.GraphJson.Should().Contain("nodes");
    }
}
