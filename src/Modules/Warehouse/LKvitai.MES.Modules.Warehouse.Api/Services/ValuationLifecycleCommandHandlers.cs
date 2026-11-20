using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Marten;
using Marten.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using EfAsync = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

/// <summary>
/// Revaluation ("Уценка") document: writes one <see cref="StockValueChanged"/> of kind Revaluation.
/// The document is identified by its command id, so a repeated request is a no-op.
/// </summary>
public sealed class RevaluationCommandHandler : IRequestHandler<RevaluationCommand, Result>
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IDocumentStore _documentStore;
    private readonly ICurrentUserService _currentUserService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<RevaluationCommandHandler> _logger;

    public RevaluationCommandHandler(
        WarehouseDbContext dbContext,
        IDocumentStore documentStore,
        ICurrentUserService currentUserService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<RevaluationCommandHandler> logger)
    {
        _dbContext = dbContext;
        _documentStore = documentStore;
        _currentUserService = currentUserService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task<Result> Handle(RevaluationCommand request, CancellationToken cancellationToken)
    {
        var requestValidation = ValuationRevaluationPolicy.ValidateRequest(request);
        if (!requestValidation.IsSuccess)
        {
            return requestValidation;
        }

        var item = await EfAsync.FirstOrDefaultAsync(
            _dbContext.Items.AsNoTracking(), x => x.Id == request.ItemId, cancellationToken);
        if (item is null)
        {
            return Result.Fail(DomainErrorCodes.NotFound, $"Item {request.ItemId} not found.");
        }

        var documentId = request.CommandId.ToString("N");
        var streamId = ItemValuation.StreamIdFor(item.Id);
        IReadOnlyList<StockValueChanged> written = [];

        try
        {
            await StockValueRetry.RunAsync(async () =>
            {
                await using var session = _documentStore.LightweightSession();
                var streamState = await session.Events.FetchStreamStateAsync(streamId, cancellationToken);
                if (streamState is null)
                {
                    throw new DomainException(DomainErrorCodes.ValidationError, $"Item {item.Id} has no valued stock.");
                }

                var events = await session.Events.FetchStreamAsync(streamId, token: cancellationToken);
                if (events.Any(x => x.Data is StockValueChanged { Kind: StockValueKinds.Revaluation } v && v.DocumentId == documentId))
                {
                    return;
                }

                var aggregate = await session.Events.AggregateStreamAsync<ItemValuation>(streamId, token: cancellationToken)
                                ?? new ItemValuation();
                var evt = aggregate.Revalue(
                    item.Id,
                    request.NewUnitPrice,
                    new StockValueDocument(
                        "Revaluation", documentId, $"UC-{DateTime.UtcNow:yyyyMMdd}-{documentId[..8]}", request.Reason.Trim()));

                var currentUser = _httpContextAccessor.HttpContext?.User;
                var canApprove = currentUser?.IsInRole(WarehouseRoles.WarehouseManager) == true ||
                                 currentUser?.IsInRole(WarehouseRoles.WarehouseAdmin) == true;
                var approval = ValuationRevaluationPolicy.ValidateApproval(-evt.ValueDeltaEur, request.ApprovedBy, canApprove);
                if (!approval.IsSuccess)
                {
                    throw new DomainException(approval.ErrorCode ?? DomainErrorCodes.ValidationError, approval.ErrorDetail ?? approval.Error);
                }

                session.Events.Append(streamId, streamState.Version + 1, evt);
                await session.SaveChangesAsync(cancellationToken);
                written = [evt];
            }, cancellationToken);
        }
        catch (EventStreamUnexpectedMaxEventIdException)
        {
            return Result.Fail(DomainErrorCodes.ConcurrencyConflict, "Valuation update conflicted with another request.");
        }
        catch (DomainException ex)
        {
            return Result.Fail(ex.ErrorCode, ex.Message);
        }

        await StockValueWriter.ProjectAsync(_dbContext, _logger, written, cancellationToken);
        _logger.LogInformation(
            "Revaluation {DocumentId}. ItemId {ItemId}, NewUnitPrice {Price}, Operator {Operator}",
            documentId, item.Id, request.NewUnitPrice, _currentUserService.GetCurrentUserId());
        return Result.Ok();
    }
}
