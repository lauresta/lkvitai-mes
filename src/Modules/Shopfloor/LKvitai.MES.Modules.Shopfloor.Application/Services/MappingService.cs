using FluentValidation;
using LKvitai.MES.Modules.Shopfloor.Application.Exceptions;
using LKvitai.MES.Modules.Shopfloor.Application.Ports;
using LKvitai.MES.Modules.Shopfloor.Application.Validation;
using LKvitai.MES.Modules.Shopfloor.Contracts.Mappings;
using LKvitai.MES.Modules.Shopfloor.Domain.Entities;

namespace LKvitai.MES.Modules.Shopfloor.Application.Services;

public sealed class MappingService : IMappingService
{
    private readonly IProductTypeMappingRepository _mappings;
    private readonly ILegacyProductTypeRepository _legacy;
    private readonly IWorkflowTemplateRepository _workflows;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IValidator<BulkAssignMappingRequest> _bulkValidator;

    public MappingService(
        IProductTypeMappingRepository mappings,
        ILegacyProductTypeRepository legacy,
        IWorkflowTemplateRepository workflows,
        IUnitOfWork unitOfWork,
        IValidator<BulkAssignMappingRequest> bulkValidator)
    {
        _mappings = mappings;
        _legacy = legacy;
        _workflows = workflows;
        _unitOfWork = unitOfWork;
        _bulkValidator = bulkValidator;
    }

    public async Task<CoverageSummaryDto> GetCoverageAsync(CancellationToken cancellationToken)
    {
        var total = await _legacy.CountActiveAsync(cancellationToken).ConfigureAwait(false);
        var mapped = await _legacy.CountActiveMappedAsync(cancellationToken).ConfigureAwait(false);
        var families = await _mappings.CountDistinctMappedTemplatesAsync(cancellationToken).ConfigureAwait(false);
        return new CoverageSummaryDto(total, mapped, Math.Max(0, total - mapped), families);
    }

    public async Task BulkAssignAsync(BulkAssignMappingRequest request, CancellationToken cancellationToken)
    {
        await _bulkValidator.EnsureValidAsync(request, cancellationToken).ConfigureAwait(false);

        var template = await _workflows.GetAsync(request.WorkflowTemplateId, cancellationToken).ConfigureAwait(false);
        if (template is null)
        {
            throw new ShopfloorNotFoundException(
                $"Workflow template '{request.WorkflowTemplateId}' was not found.");
        }

        var codes = request.LegacyProductTypeCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var existingCodes = await _legacy.GetExistingCodesAsync(codes, cancellationToken).ConfigureAwait(false);
        var missing = codes.Except(existingCodes, StringComparer.Ordinal).ToList();
        if (missing.Count > 0)
        {
            throw new ShopfloorValidationException(
                "Unknown legacy product type codes: " + string.Join(", ", missing));
        }

        var existingMaps = (await _mappings.GetManyAsync(codes, cancellationToken).ConfigureAwait(false))
            .ToDictionary(m => m.LegacyProductTypeCode, StringComparer.Ordinal);

        var now = DateTimeOffset.UtcNow;
        foreach (var code in codes)
        {
            if (existingMaps.TryGetValue(code, out var map))
            {
                map.Reassign(request.WorkflowTemplateId, now);
            }
            else
            {
                _mappings.Add(new ProductTypeWorkflowMap(code, request.WorkflowTemplateId, now));
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(string legacyProductTypeCode, CancellationToken cancellationToken)
    {
        var code = legacyProductTypeCode?.Trim() ?? string.Empty;
        var map = await _mappings.GetAsync(code, cancellationToken).ConfigureAwait(false);
        if (map is null)
        {
            throw new ShopfloorNotFoundException($"No mapping found for legacy product type '{code}'.");
        }

        _mappings.Remove(map);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
