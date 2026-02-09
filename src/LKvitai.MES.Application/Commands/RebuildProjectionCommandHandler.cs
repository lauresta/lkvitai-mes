using LKvitai.MES.Application.Projections;
using LKvitai.MES.SharedKernel;
using MediatR;

namespace LKvitai.MES.Application.Commands;

public class RebuildProjectionCommandHandler
    : IRequestHandler<RebuildProjectionCommand, Result<ProjectionRebuildReport>>
{
    private static readonly SemaphoreSlim RebuildLock = new(1, 1);

    private readonly IProjectionRebuildService _projectionRebuildService;

    public RebuildProjectionCommandHandler(IProjectionRebuildService projectionRebuildService)
    {
        _projectionRebuildService = projectionRebuildService;
    }

    public async Task<Result<ProjectionRebuildReport>> Handle(
        RebuildProjectionCommand request,
        CancellationToken cancellationToken)
    {
        var acquired = await RebuildLock.WaitAsync(0, cancellationToken);
        if (!acquired)
        {
            return Result<ProjectionRebuildReport>.Fail(
                DomainErrorCodes.IdempotencyInProgress,
                "Projection rebuild is already in progress.");
        }

        try
        {
            return await _projectionRebuildService.RebuildProjectionAsync(
                request.ProjectionName,
                request.Verify,
                cancellationToken);
        }
        finally
        {
            RebuildLock.Release();
        }
    }
}
