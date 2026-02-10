using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/locations")]
public sealed class LocationsController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public LocationsController(WarehouseDbContext dbContext)
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

        var query = _dbContext.Locations
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
                x.Barcode.ToLower().Contains(normalized));
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
            .Select(x => new LocationListItemDto(
                x.Id,
                x.Code,
                x.Barcode,
                x.Type,
                x.ParentLocationId,
                x.IsVirtual,
                x.MaxWeight,
                x.MaxVolume,
                x.Status,
                x.ZoneType,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<LocationListItemDto>(rows, totalCount, pageNumber, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] UpsertLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationFailure("Field 'code' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            return ValidationFailure("Field 'barcode' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return ValidationFailure("Field 'type' is required.");
        }

        if (request.ParentLocationId.HasValue)
        {
            var parentExists = await _dbContext.Locations
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.ParentLocationId.Value, cancellationToken);
            if (!parentExists)
            {
                return ValidationFailure($"Parent location '{request.ParentLocationId.Value}' does not exist.");
            }
        }

        var normalizedCode = request.Code.Trim();
        var normalizedBarcode = request.Barcode.Trim();

        var duplicateCode = await _dbContext.Locations
            .AsNoTracking()
            .AnyAsync(x => x.Code == normalizedCode, cancellationToken);
        if (duplicateCode)
        {
            return ValidationFailure($"Location code '{normalizedCode}' already exists.");
        }

        var duplicateBarcode = await _dbContext.Locations
            .AsNoTracking()
            .AnyAsync(x => x.Barcode == normalizedBarcode, cancellationToken);
        if (duplicateBarcode)
        {
            return ValidationFailure($"Location barcode '{normalizedBarcode}' already exists.");
        }

        var entity = new Location
        {
            Code = normalizedCode,
            Barcode = normalizedBarcode,
            Type = request.Type.Trim(),
            ParentLocationId = request.ParentLocationId,
            IsVirtual = request.IsVirtual,
            MaxWeight = request.MaxWeight,
            MaxVolume = request.MaxVolume,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim(),
            ZoneType = request.ZoneType?.Trim()
        };

        _dbContext.Locations.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created($"/api/warehouse/v1/locations/{entity.Id}", new LocationListItemDto(
            entity.Id,
            entity.Code,
            entity.Barcode,
            entity.Type,
            entity.ParentLocationId,
            entity.IsVirtual,
            entity.MaxWeight,
            entity.MaxVolume,
            entity.Status,
            entity.ZoneType,
            entity.CreatedAt,
            entity.UpdatedAt));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] UpsertLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationFailure("Field 'code' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            return ValidationFailure("Field 'barcode' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return ValidationFailure("Field 'type' is required.");
        }

        var entity = await _dbContext.Locations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Location '{id}' does not exist."));
        }

        if (request.ParentLocationId == id)
        {
            return ValidationFailure("Location cannot reference itself as parent.");
        }

        if (request.ParentLocationId.HasValue)
        {
            var parentExists = await _dbContext.Locations
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.ParentLocationId.Value, cancellationToken);
            if (!parentExists)
            {
                return ValidationFailure($"Parent location '{request.ParentLocationId.Value}' does not exist.");
            }
        }

        var normalizedCode = request.Code.Trim();
        var normalizedBarcode = request.Barcode.Trim();

        if (entity.IsVirtual &&
            (!string.Equals(entity.Code, normalizedCode, StringComparison.Ordinal) ||
             !string.Equals(entity.Barcode, normalizedBarcode, StringComparison.Ordinal)))
        {
            return UnprocessableFailure("Virtual locations cannot modify Code or Barcode.");
        }

        var duplicateCode = await _dbContext.Locations
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Code == normalizedCode, cancellationToken);
        if (duplicateCode)
        {
            return ValidationFailure($"Location code '{normalizedCode}' already exists.");
        }

        var duplicateBarcode = await _dbContext.Locations
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Barcode == normalizedBarcode, cancellationToken);
        if (duplicateBarcode)
        {
            return ValidationFailure($"Location barcode '{normalizedBarcode}' already exists.");
        }

        entity.Code = normalizedCode;
        entity.Barcode = normalizedBarcode;
        entity.Type = request.Type.Trim();
        entity.ParentLocationId = request.ParentLocationId;
        entity.MaxWeight = request.MaxWeight;
        entity.MaxVolume = request.MaxVolume;
        entity.Status = string.IsNullOrWhiteSpace(request.Status) ? entity.Status : request.Status.Trim();
        entity.ZoneType = request.ZoneType?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);
        return Ok(new LocationListItemDto(
            entity.Id,
            entity.Code,
            entity.Barcode,
            entity.Type,
            entity.ParentLocationId,
            entity.IsVirtual,
            entity.MaxWeight,
            entity.MaxVolume,
            entity.Status,
            entity.ZoneType,
            entity.CreatedAt,
            entity.UpdatedAt));
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

    private ObjectResult UnprocessableFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity
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

    public sealed record UpsertLocationRequest(
        string Code,
        string Barcode,
        string Type,
        int? ParentLocationId,
        bool IsVirtual,
        decimal? MaxWeight,
        decimal? MaxVolume,
        string? Status,
        string? ZoneType);

    public sealed record LocationListItemDto(
        int Id,
        string Code,
        string Barcode,
        string Type,
        int? ParentLocationId,
        bool IsVirtual,
        decimal? MaxWeight,
        decimal? MaxVolume,
        string Status,
        string? ZoneType,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
