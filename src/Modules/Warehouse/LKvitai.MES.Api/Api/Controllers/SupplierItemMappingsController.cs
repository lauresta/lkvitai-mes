using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/supplier-item-mappings")]
public sealed class SupplierItemMappingsController : ControllerBase
{
    private readonly WarehouseDbContext _dbContext;

    public SupplierItemMappingsController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? search,
        [FromQuery] int? supplierId,
        [FromQuery] int? itemId,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _dbContext.SupplierItemMappings
            .AsNoTracking()
            .Include(x => x.Supplier)
            .Include(x => x.Item)
            .AsQueryable();

        if (supplierId.HasValue)
        {
            query = query.Where(x => x.SupplierId == supplierId.Value);
        }

        if (itemId.HasValue)
        {
            query = query.Where(x => x.ItemId == itemId.Value);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.SupplierSKU.ToLower().Contains(normalized) ||
                (x.Supplier != null && x.Supplier.Code.ToLower().Contains(normalized)) ||
                (x.Supplier != null && x.Supplier.Name.ToLower().Contains(normalized)) ||
                (x.Item != null && x.Item.InternalSKU.ToLower().Contains(normalized)) ||
                (x.Item != null && x.Item.Name.ToLower().Contains(normalized)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.SupplierId)
            .ThenBy(x => x.SupplierSKU)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SupplierItemMappingListItemDto(
                x.Id,
                x.SupplierId,
                x.Supplier != null ? x.Supplier.Code : string.Empty,
                x.Supplier != null ? x.Supplier.Name : string.Empty,
                x.SupplierSKU,
                x.ItemId,
                x.Item != null ? x.Item.InternalSKU : string.Empty,
                x.Item != null ? x.Item.Name : string.Empty,
                x.LeadTimeDays,
                x.MinOrderQty,
                x.PricePerUnit))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<SupplierItemMappingListItemDto>(rows, totalCount, pageNumber, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] UpsertSupplierItemMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRequestAsync(request, null, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var entity = new SupplierItemMapping
        {
            SupplierId = request.SupplierId,
            SupplierSKU = request.SupplierSKU.Trim(),
            ItemId = request.ItemId,
            LeadTimeDays = request.LeadTimeDays,
            MinOrderQty = request.MinOrderQty,
            PricePerUnit = request.PricePerUnit
        };

        _dbContext.SupplierItemMappings.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Created(
            $"/api/warehouse/v1/supplier-item-mappings/{entity.Id}",
            new SupplierItemMappingListItemDto(
                entity.Id,
                entity.SupplierId,
                string.Empty,
                string.Empty,
                entity.SupplierSKU,
                entity.ItemId,
                string.Empty,
                string.Empty,
                entity.LeadTimeDays,
                entity.MinOrderQty,
                entity.PricePerUnit));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] UpsertSupplierItemMappingRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.SupplierItemMappings
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Supplier-item mapping '{id}' does not exist."));
        }

        var validation = await ValidateRequestAsync(request, id, cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        entity.SupplierId = request.SupplierId;
        entity.SupplierSKU = request.SupplierSKU.Trim();
        entity.ItemId = request.ItemId;
        entity.LeadTimeDays = request.LeadTimeDays;
        entity.MinOrderQty = request.MinOrderQty;
        entity.PricePerUnit = request.PricePerUnit;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new SupplierItemMappingListItemDto(
            entity.Id,
            entity.SupplierId,
            string.Empty,
            string.Empty,
            entity.SupplierSKU,
            entity.ItemId,
            string.Empty,
            string.Empty,
            entity.LeadTimeDays,
            entity.MinOrderQty,
            entity.PricePerUnit));
    }

    private async Task<ObjectResult?> ValidateRequestAsync(
        UpsertSupplierItemMappingRequest request,
        int? existingId,
        CancellationToken cancellationToken)
    {
        if (request.SupplierId <= 0)
        {
            return ValidationFailure("Field 'supplierId' must be greater than 0.");
        }

        if (request.ItemId <= 0)
        {
            return ValidationFailure("Field 'itemId' must be greater than 0.");
        }

        if (string.IsNullOrWhiteSpace(request.SupplierSKU))
        {
            return ValidationFailure("Field 'supplierSKU' is required.");
        }

        if (request.LeadTimeDays.HasValue && request.LeadTimeDays.Value < 0)
        {
            return ValidationFailure("Field 'leadTimeDays' must be greater than or equal to 0.");
        }

        if (request.MinOrderQty.HasValue && request.MinOrderQty.Value <= 0)
        {
            return ValidationFailure("Field 'minOrderQty' must be greater than 0.");
        }

        if (request.PricePerUnit.HasValue && request.PricePerUnit.Value <= 0)
        {
            return ValidationFailure("Field 'pricePerUnit' must be greater than 0.");
        }

        var supplierExists = await _dbContext.Suppliers
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.SupplierId, cancellationToken);
        if (!supplierExists)
        {
            return ValidationFailure($"Supplier '{request.SupplierId}' does not exist.");
        }

        var itemExists = await _dbContext.Items
            .AsNoTracking()
            .AnyAsync(x => x.Id == request.ItemId, cancellationToken);
        if (!itemExists)
        {
            return ValidationFailure($"Item '{request.ItemId}' does not exist.");
        }

        var normalizedSupplierSku = request.SupplierSKU.Trim();
        var duplicate = await _dbContext.SupplierItemMappings
            .AsNoTracking()
            .AnyAsync(
                x => x.SupplierId == request.SupplierId &&
                     x.SupplierSKU == normalizedSupplierSku &&
                     (!existingId.HasValue || x.Id != existingId.Value),
                cancellationToken);
        if (duplicate)
        {
            return ValidationFailure(
                $"Mapping for supplier '{request.SupplierId}' and supplier SKU '{normalizedSupplierSku}' already exists.");
        }

        return null;
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

    public sealed record UpsertSupplierItemMappingRequest(
        int SupplierId,
        string SupplierSKU,
        int ItemId,
        int? LeadTimeDays,
        decimal? MinOrderQty,
        decimal? PricePerUnit);

    public sealed record SupplierItemMappingListItemDto(
        int Id,
        int SupplierId,
        string SupplierCode,
        string SupplierName,
        string SupplierSKU,
        int ItemId,
        string ItemSKU,
        string ItemName,
        int? LeadTimeDays,
        decimal? MinOrderQty,
        decimal? PricePerUnit);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
