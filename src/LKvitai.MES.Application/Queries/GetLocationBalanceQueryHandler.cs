using LKvitai.MES.Application.Ports;
using LKvitai.MES.SharedKernel;
using MediatR;

namespace LKvitai.MES.Application.Queries;

/// <summary>
/// Handler for GetLocationBalanceQuery
/// </summary>
public class GetLocationBalanceQueryHandler : IRequestHandler<GetLocationBalanceQuery, Result<LocationBalanceDto?>>
{
    private readonly ILocationBalanceRepository _repository;
    
    public GetLocationBalanceQueryHandler(ILocationBalanceRepository repository)
    {
        _repository = repository;
    }
    
    public async Task<Result<LocationBalanceDto?>> Handle(
        GetLocationBalanceQuery request,
        CancellationToken cancellationToken)
    {
        var balance = await _repository.GetBalanceAsync(
            request.WarehouseId,
            request.Location,
            request.SKU,
            cancellationToken);
        
        return Result<LocationBalanceDto?>.Ok(balance);
    }
}
