using FluentValidation;
using LKvitai.MES.Modules.Shopfloor.Application.Exceptions;
using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Application.Validation;
using LKvitai.MES.Modules.Shopfloor.Contracts.Reference;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

public sealed class WorkCenterService : IWorkCenterService
{
    private readonly IWorkCenterRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<CreateWorkCenterRequest> _createValidator;
    private readonly IValidator<UpdateWorkCenterRequest> _updateValidator;

    public WorkCenterService(
        IWorkCenterRepository repository,
        IUnitOfWork unitOfWork,
        IValidator<CreateWorkCenterRequest> createValidator,
        IValidator<UpdateWorkCenterRequest> updateValidator)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<WorkCenterDto>> ListAsync(CancellationToken cancellationToken)
    {
        var centers = await _repository.ListAsync(cancellationToken).ConfigureAwait(false);
        return centers.Select(Map).ToList();
    }

    public async Task<WorkCenterDto> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var center = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);
        return Map(center);
    }

    public async Task<WorkCenterDto> CreateAsync(CreateWorkCenterRequest request, CancellationToken cancellationToken)
    {
        await _createValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        if (await _repository.CodeExistsAsync(request.Code.Trim(), null, cancellationToken).ConfigureAwait(false))
        {
            throw Conflict(request.Code);
        }

        var center = new WorkCenter(Guid.NewGuid(), request.Code, request.Name);
        _repository.Add(center);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(center);
    }

    public async Task<WorkCenterDto> UpdateAsync(Guid id, UpdateWorkCenterRequest request, CancellationToken cancellationToken)
    {
        await _updateValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var center = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);

        if (await _repository.CodeExistsAsync(request.Code.Trim(), id, cancellationToken).ConfigureAwait(false))
        {
            throw Conflict(request.Code);
        }

        center.Recode(request.Code);
        center.Rename(request.Name);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Map(center);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var center = await _repository.GetAsync(id, cancellationToken).ConfigureAwait(false)
            ?? throw NotFound(id);

        if (await _repository.HasStationsAsync(id, cancellationToken).ConfigureAwait(false))
        {
            throw new ShopfloorConflictException(
                $"Work center '{center.Code}' still has work stations and cannot be deleted.");
        }

        _repository.Remove(center);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WorkCenterDto Map(WorkCenter c) => new(c.Id, c.Code, c.Name);

    private static ShopfloorNotFoundException NotFound(Guid id)
        => new($"Work center '{id}' was not found.");

    private static ShopfloorConflictException Conflict(string code)
        => new($"A work center with code '{code}' already exists.");
}
