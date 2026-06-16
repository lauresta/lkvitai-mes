using FluentAssertions;
using LKvitai.MES.Modules.Shopfloor.Application.Exceptions;
using LKvitai.MES.Modules.Shopfloor.Application.Workflows;
using LKvitai.MES.Modules.Shopfloor.Contracts.Workflows;
using LKvitai.MES.Modules.Shopfloor.Domain.Workflows;
using Xunit;

namespace LKvitai.MES.Tests.Shopfloor.Unit;

public class WorkflowGraphMapperTests
{
    [Fact]
    public void DefaultGraph_RoundTrips_AndIsLenientValid()
    {
        var dto = WorkflowGraphMapper.DefaultGraph();

        var json = WorkflowGraphMapper.Serialize(dto);
        var back = WorkflowGraphMapper.Deserialize(json);

        back.Nodes.Should().HaveCount(2);
        back.Edges.Should().ContainSingle();

        var domain = WorkflowGraphMapper.ToDomain(back);
        WorkflowGraphValidator.ValidateLenient(domain).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Deserialize_Empty_Throws()
    {
        var act = () => WorkflowGraphMapper.Deserialize("  ");
        act.Should().Throw<ShopfloorValidationException>();
    }

    [Fact]
    public void Deserialize_InvalidJson_Throws()
    {
        var act = () => WorkflowGraphMapper.Deserialize("{ not json ");
        act.Should().Throw<ShopfloorValidationException>();
    }

    [Fact]
    public void ToDomain_UnknownKind_Throws()
    {
        var dto = new WorkflowGraphDto(
            new[]
            {
                new WorkflowNodeDto("x", "decision", "x", new WorkflowNodePositionDto(0, 0), null, null, null),
            },
            Array.Empty<WorkflowEdgeDto>());

        var act = () => WorkflowGraphMapper.ToDomain(dto);
        act.Should().Throw<ShopfloorValidationException>()
            .WithMessage("*Unknown node kind*");
    }

    [Fact]
    public void ToDomain_MapsTaskFields()
    {
        var station = Guid.NewGuid();
        var dto = new WorkflowGraphDto(
            new[]
            {
                new WorkflowNodeDto("cut", WorkflowNodeKinds.Task, "Cut", new WorkflowNodePositionDto(10, 20), station, 90, "CUT"),
            },
            Array.Empty<WorkflowEdgeDto>());

        var domain = WorkflowGraphMapper.ToDomain(dto);

        var node = domain.Nodes.Single();
        node.Kind.Should().Be(WorkflowNodeKind.Task);
        node.WorkStationId.Should().Be(station);
        node.DurationSec.Should().Be(90);
        node.TaskTypeCode.Should().Be("CUT");
    }
}
