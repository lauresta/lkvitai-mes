using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.SharedKernel;
using MediatR;

namespace LKvitai.MES.Modules.Warehouse.Application.Queries;

/// <summary>
/// Handler for GetAvailableStockQuery.
/// Delegates to the IAvailableStockRepository port.
/// </summary>
public class GetAvailableStockQueryHandler : IRequestHandler<GetAvailableStockQuery, Result<AvailableStockDto?>>
{
    private readonly IAvailableStockRepository _repository;

    public GetAvailableStockQueryHandler(IAvailableStockRepository repository)
    {
        _repository = repository;
    }

    public async Task<Result<AvailableStockDto?>> Handle(
        GetAvailableStockQuery request,
        CancellationToken cancellationToken)
    {
        var stock = await _repository.GetAvailableStockAsync(
            request.WarehouseId,
            request.Location,
            request.SKU,
            cancellationToken);

        return Result<AvailableStockDto?>.Ok(stock);
    }
}
