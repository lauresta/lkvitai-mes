using FluentValidation;
using LKvitai.MES.Modules.Shopfloor.Application.Exceptions;
using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Application.Validation;
using LKvitai.MES.Modules.Shopfloor.Contracts.Reference;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

public sealed class WorkStationService : IWorkStationService
{
    private readonly IWorkStationRepository _repository;
    private readonly IWorkCenterRepository _workCenterRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateWorkStationRequest> _createValidator;
    private readonly IValidator<UpdateWorkStationRequest> _updateValidator;

    public WorkStationService(
        IWorkStationRepository repository,
        IWorkCenterRepository workCenterRepository,
        IUnitOfWork unitOfWork,
        IValidator<CreateWorkStationRequest> createValidator,
        IValidator<UpdateWorkStationRequest> updateValidator)
    {
        _repository = repository;
        _workCenterRepository = workCenterRepository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<WorkStationDto>> ListAsync(bool activeOnly, CancellationToken cancellationToken)
    {
        var stations = activeOnly
            ? await _repository.ListActiveAsync(cancellationToken).ConfigureAwait(false)
            : await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return stations.Select(Map).ToList();
    }

    public async Task<WorkStationDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var station = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);
        return Map(station);
    }

    public async Task<WorkStationDto> CreateAsync(CreateWorkStationRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);
        await EnsureWorkCenterExistsAsync(request.WorkCenterId, cancellationToken).ConfigureAwait(false);

        if (await _repository.CodeExistsAsync(request.Code.Trim(), null, cancellationToken).ConfigureAwait(false))
        {
            throw Conflict(request.Code);
        }

        var station = new WorkStation(
            Guid.NewGuid(), request.Code, request.Name, request.WorkCenterId, request.WipLimit, request.IsActive);
        _repository.Add(station);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(station.Id, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkStationDto> UpdateAsync(Guid id, UpdateWorkStationRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var existing = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);
        await EnsureWorkCenterExistsAsync(request.WorkCenterId, cancellationToken).ConfigureAwait(false);

        if (await _repository.CodeExistsAsync(request.Code.Trim(), id, cancellationToken).ConfigureAwait(false))
        {
            throw Conflict(request.Code);
        }

        existing.Station.Update(request.Code, request.Name, request.WorkCenterId, request.WipLimit, request.IsActive);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return await GetAsync(id, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);
        _repository.Remove(existing.Station);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task EnsureWorkCenterExistsAsync(Guid workCenterId, CancellationToken cancellationToken)
    {
        var center = await _workCenterRepository.GetAsync(workCenterId, cancellationToken).ConfigureAwait(false);
        if (center is null)
        {
            throw new ShopfloorValidationException($"Work center '{workCenterId}' does not exist.");
        }
    }

    private static WorkStationDto Map(WorkStationWithCenter s) => new(
        s.Station.Id, s.Station.Code, s.Station.Name, s.Station.WorkCenterId,
        s.WorkCenterName, s.Station.WipLimit, s.Station.IsActive);

    private static ShopfloorNotFoundException NotFound(Guid id)
        => new($"Work station '{id}' was not found.");

    private static ShopfloorConflictException Conflict(string code)
        => new($"A work station with code '{code}' already exists.");
}
