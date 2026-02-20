using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/items")]
public sealed class ItemsController : ControllerBase
{
    private const string GetItemByIdRouteName = "GetItemById";

    private readonly WarehouseDbContext _dbContext;
    private readonly ISkuGenerationService _skuGenerationService;

    public ItemsController(
        WarehouseDbContext dbContext,
        ISkuGenerationService skuGenerationService)
    {
        _dbContext = dbContext;
        _skuGenerationService = skuGenerationService;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? search,
        [FromQuery] int? categoryId,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 500);
        pageNumber = Math.Max(1, pageNumber);

        var query = _dbContext.Items
            .AsNoTracking()
            .Include(i => i.Category)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(i =>
                i.InternalSKU.ToLower().Contains(normalized) ||
                i.Name.ToLower().Contains(normalized));
        }

        if (categoryId.HasValue)
        {
            query = query.Where(i => i.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(i => i.Status == status.Trim());
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderBy(i => i.InternalSKU)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new ItemListItemDto(
                i.Id,
                i.InternalSKU,
                i.Name,
                i.CategoryId,
                i.Category != null ? i.Category.Name : string.Empty,
                i.BaseUoM,
                i.Status,
                i.RequiresLotTracking,
                i.RequiresQC,
                i.PrimaryBarcode,
                i.CreatedAt,
                i.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<ItemListItemDto>(items, totalCount, pageNumber, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] CreateItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationFailure("Field 'name' is required.");
        }

        var categoryExists = await _dbContext.ItemCategories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return ValidationFailure($"Category '{request.CategoryId}' does not exist.");
        }

        var uomExists = await _dbContext.UnitOfMeasures
            .AnyAsync(u => u.Code == request.BaseUoM, cancellationToken);
        if (!uomExists)
        {
            return ValidationFailure($"UoM '{request.BaseUoM}' does not exist.");
        }

        var sku = string.IsNullOrWhiteSpace(request.InternalSKU)
            ? await _skuGenerationService.GenerateNextSkuAsync(request.CategoryId, cancellationToken)
            : request.InternalSKU.Trim();

        var item = new Item
        {
            InternalSKU = sku,
            Name = request.Name.Trim(),
            Description = request.Description?.Trim(),
            CategoryId = request.CategoryId,
            BaseUoM = request.BaseUoM.Trim(),
            Weight = request.Weight,
            Volume = request.Volume,
            RequiresLotTracking = request.RequiresLotTracking,
            RequiresQC = request.RequiresQC,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim(),
            PrimaryBarcode = request.PrimaryBarcode?.Trim(),
            ProductConfigId = request.ProductConfigId?.Trim()
        };

        _dbContext.Items.Add(item);
        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException)
        {
            return Failure(Result.Fail(
                DomainErrorCodes.ConcurrencyConflict,
                $"Item with SKU '{sku}' already exists."));
        }

        await Cache.RemoveAsync($"item:{item.Id}", cancellationToken);

        return CreatedAtRoute(
            GetItemByIdRouteName,
            new { id = item.Id },
            new ItemCreatedDto(item.Id, item.InternalSKU, item.Name, item.CreatedAt));
    }

    [HttpGet("{id:int}", Name = GetItemByIdRouteName)]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"item:{id}";
        var cached = await Cache.GetAsync<Item>(cacheKey, cancellationToken);
        if (cached is not null)
        {
            return Ok(cached);
        }

        var item = await _dbContext.Items
            .AsNoTracking()
            .Include(i => i.Barcodes)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        await Cache.SetAsync(cacheKey, item, TimeSpan.FromHours(1), cancellationToken);
        return Ok(item);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] UpdateItemRequestDto request,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return ValidationFailure("Field 'name' is required.");
        }

        var categoryExists = await _dbContext.ItemCategories
            .AnyAsync(c => c.Id == request.CategoryId, cancellationToken);
        if (!categoryExists)
        {
            return ValidationFailure($"Category '{request.CategoryId}' does not exist.");
        }

        var uomExists = await _dbContext.UnitOfMeasures
            .AnyAsync(u => u.Code == request.BaseUoM, cancellationToken);
        if (!uomExists)
        {
            return ValidationFailure($"UoM '{request.BaseUoM}' does not exist.");
        }

        item.Name = request.Name.Trim();
        item.Description = request.Description?.Trim();
        item.CategoryId = request.CategoryId;
        item.BaseUoM = request.BaseUoM.Trim();
        item.Weight = request.Weight;
        item.Volume = request.Volume;
        item.RequiresLotTracking = request.RequiresLotTracking;
        item.RequiresQC = request.RequiresQC;
        item.Status = string.IsNullOrWhiteSpace(request.Status) ? item.Status : request.Status.Trim();
        item.PrimaryBarcode = request.PrimaryBarcode?.Trim();
        item.ProductConfigId = request.ProductConfigId?.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"item:{id}", cancellationToken);

        return Ok(new ItemUpdatedDto(item.Id, item.InternalSKU, item.Name, item.Status, item.UpdatedAt));
    }

    [HttpPost("{id:int}/deactivate")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> DeactivateAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        item.Status = "Discontinued";
        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"item:{id}", cancellationToken);

        return Ok(new ItemUpdatedDto(item.Id, item.InternalSKU, item.Name, item.Status, item.UpdatedAt));
    }

    [HttpGet("{id:int}/barcodes")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetBarcodesAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        var barcodes = await _dbContext.ItemBarcodes
            .AsNoTracking()
            .Where(b => b.ItemId == id)
            .OrderByDescending(b => b.IsPrimary)
            .ThenBy(b => b.Barcode)
            .Select(b => new ItemBarcodeDto(b.Id, b.Barcode, b.BarcodeType, b.IsPrimary))
            .ToListAsync(cancellationToken);

        return Ok(new ItemBarcodesResponse(id, item.InternalSKU, barcodes));
    }

    [HttpPost("{id:int}/barcodes")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> AddBarcodeAsync(
        int id,
        [FromBody] AddBarcodeRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            return ValidationFailure("Field 'barcode' is required.");
        }

        var item = await _dbContext.Items.FirstOrDefaultAsync(i => i.Id == id, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        var barcode = request.Barcode.Trim();
        var barcodeExists = await _dbContext.ItemBarcodes
            .AsNoTracking()
            .AnyAsync(b => b.Barcode == barcode, cancellationToken);
        if (barcodeExists)
        {
            return Failure(Result.Fail(
                DomainErrorCodes.ConcurrencyConflict,
                $"Barcode '{barcode}' already exists."));
        }

        if (request.IsPrimary)
        {
            var existingPrimary = await _dbContext.ItemBarcodes
                .Where(x => x.ItemId == id && x.IsPrimary)
                .ToListAsync(cancellationToken);
            foreach (var current in existingPrimary)
            {
                current.IsPrimary = false;
            }
            item.PrimaryBarcode = barcode;
        }

        var entity = new ItemBarcode
        {
            ItemId = id,
            Barcode = barcode,
            BarcodeType = string.IsNullOrWhiteSpace(request.BarcodeType) ? "Code128" : request.BarcodeType.Trim(),
            IsPrimary = request.IsPrimary
        };

        _dbContext.ItemBarcodes.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"item:{id}", cancellationToken);

        return Ok(new ItemBarcodeDto(entity.Id, entity.Barcode, entity.BarcodeType, entity.IsPrimary));
    }

    private ICacheService Cache => HttpContext?.RequestServices?.GetService<ICacheService>() ?? new LKvitai.MES.Modules.Warehouse.Infrastructure.Caching.NoOpCacheService();

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

    public sealed record CreateItemRequestDto(
        string? InternalSKU,
        string Name,
        string? Description,
        int CategoryId,
        string BaseUoM,
        decimal? Weight,
        decimal? Volume,
        bool RequiresLotTracking,
        bool RequiresQC,
        string? Status,
        string? PrimaryBarcode,
        string? ProductConfigId);

    public sealed record UpdateItemRequestDto(
        string Name,
        string? Description,
        int CategoryId,
        string BaseUoM,
        decimal? Weight,
        decimal? Volume,
        bool RequiresLotTracking,
        bool RequiresQC,
        string? Status,
        string? PrimaryBarcode,
        string? ProductConfigId);

    public sealed record ItemCreatedDto(int Id, string InternalSKU, string Name, DateTimeOffset CreatedAt);
    public sealed record ItemUpdatedDto(int Id, string InternalSKU, string Name, string Status, DateTimeOffset? UpdatedAt);

    public sealed record ItemListItemDto(
        int Id,
        string InternalSKU,
        string Name,
        int CategoryId,
        string CategoryName,
        string BaseUoM,
        string Status,
        bool RequiresLotTracking,
        bool RequiresQC,
        string? PrimaryBarcode,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record AddBarcodeRequest(string Barcode, string BarcodeType, bool IsPrimary);
    public sealed record ItemBarcodeDto(int Id, string Barcode, string BarcodeType, bool IsPrimary);
    public sealed record ItemBarcodesResponse(int ItemId, string InternalSKU, IReadOnlyList<ItemBarcodeDto> Barcodes);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
