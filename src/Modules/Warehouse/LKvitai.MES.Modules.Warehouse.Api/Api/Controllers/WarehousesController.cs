using LKvitai.MES.BuildingBlocks.SharedKernel;
using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WarehouseLayoutAggregate = LKvitai.MES.Modules.Warehouse.Domain.Aggregates.WarehouseLayout;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/warehouses")]
public sealed class WarehousesController : ControllerBase
{
    private static readonly string[] AllowedStatuses = ["Active", "Inactive"];

    private readonly WarehouseDbContext _dbContext;

    public WarehousesController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] bool includeVirtual = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _dbContext.Warehouses
            .AsNoTracking()
            .AsQueryable();

        if (!includeVirtual)
        {
            query = query.Where(x => !x.IsVirtual);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.ToLower().Contains(normalized) ||
                x.Name.ToLower().Contains(normalized) ||
                (x.Description != null && x.Description.ToLower().Contains(normalized)));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new WarehouseListItemDto(
                x.WarehouseId,
                x.Code,
                x.Name,
                x.Description,
                x.IsVirtual,
                x.Status,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<WarehouseListItemDto>(rows, totalCount, pageNumber, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] UpsertWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationFailure("Field 'code' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationFailure("Field 'name' is required.");
        }

        if (!TryNormalizeStatus(request.Status, out var normalizedStatus, out var statusValidationError))
        {
            return ValidationFailure(statusValidationError!);
        }

        var normalizedCode = request.Code.Trim();
        var normalizedName = request.Name.Trim();

        var duplicateCode = await _dbContext.Warehouses
            .AsNoTracking()
            .AnyAsync(x => x.Code == normalizedCode, cancellationToken);

        if (duplicateCode)
        {
            return ValidationFailure($"Warehouse code '{normalizedCode}' already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new WarehouseLayoutAggregate
        {
            WarehouseId = Guid.NewGuid(),
            Code = normalizedCode,
            Name = normalizedName,
            Description = NormalizeDescription(request.Description),
            IsVirtual = request.IsVirtual,
            Status = normalizedStatus!,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.Warehouses.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/warehouse/v1/warehouses/{entity.WarehouseId}",
            new WarehouseListItemDto(
                entity.WarehouseId,
                entity.Code,
                entity.Name,
                entity.Description,
                entity.IsVirtual,
                entity.Status,
                entity.CreatedAt,
                entity.UpdatedAt));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateAsync(
        Guid id,
        [FromBody] UpsertWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationFailure("Field 'name' is required.");
        }

        if (!TryNormalizeStatus(request.Status, out var normalizedStatus, out var statusValidationError))
        {
            return ValidationFailure(statusValidationError!);
        }

        var entity = await _dbContext.Warehouses.FirstOrDefaultAsync(x => x.WarehouseId == id, cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Warehouse '{id}' does not exist."));
        }

        if (!string.IsNullOrWhiteSpace(request.Code) &&
            !string.Equals(entity.Code, request.Code.Trim(), StringComparison.Ordinal))
        {
            return ValidationFailure("Field 'code' is immutable and cannot be changed.");
        }

        entity.Name = request.Name.Trim();
        entity.Description = NormalizeDescription(request.Description);
        entity.IsVirtual = request.IsVirtual;
        entity.Status = normalizedStatus!;
        entity.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new WarehouseListItemDto(
            entity.WarehouseId,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.IsVirtual,
            entity.Status,
            entity.CreatedAt,
            entity.UpdatedAt));
    }

    private static bool TryNormalizeStatus(string? rawStatus, out string? normalizedStatus, out string? validationError)
    {
        validationError = null;
        normalizedStatus = null;

        if (string.IsNullOrWhiteSpace(rawStatus))
        {
            normalizedStatus = "Active";
            return true;
        }

        var trimmedStatus = rawStatus.Trim();
        var normalized = AllowedStatuses.FirstOrDefault(x => string.Equals(x, trimmedStatus, StringComparison.OrdinalIgnoreCase));

        if (normalized is null)
        {
            validationError = $"Field 'status' must be one of: {string.Join(", ", AllowedStatuses)}.";
            return false;
        }

        normalizedStatus = normalized;
        return true;
    }

    private static string? NormalizeDescription(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    public sealed record UpsertWarehouseRequest(
        string Code,
        string Name,
        string? Description,
        bool IsVirtual,
        string? Status);

    public sealed record WarehouseListItemDto(
        Guid Id,
        string Code,
        string Name,
        string? Description,
        bool IsVirtual,
        string Status,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
