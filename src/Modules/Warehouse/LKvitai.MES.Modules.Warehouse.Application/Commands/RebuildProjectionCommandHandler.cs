using LKvitai.MES.Modules.Warehouse.Application.Projections;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using MediatR;

namespace LKvitai.MES.Modules.Warehouse.Application.Commands;

public class RebuildProjectionCommandHandler
    : IRequestHandler<RebuildProjectionCommand, Result<ProjectionRebuildReport>>
{
    private readonly IProjectionRebuildService _projectionRebuildService;

    public RebuildProjectionCommandHandler(IProjectionRebuildService projectionRebuildService)
    {
        _projectionRebuildService = projectionRebuildService;
    }

    public async Task<Result<ProjectionRebuildReport>> Handle(
        RebuildProjectionCommand request,
        CancellationToken cancellationToken)
    {
        return await _projectionRebuildService.RebuildProjectionAsync(
            request.ProjectionName,
            request.Verify,
            request.ResetProgress,
            cancellationToken);
    }
}
