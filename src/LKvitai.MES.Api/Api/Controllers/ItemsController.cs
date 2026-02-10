using LKvitai.MES.Api.ErrorHandling;
using LKvitai.MES.Api.Security;
using LKvitai.MES.Application.Services;
using LKvitai.MES.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace LKvitai.MES.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/items")]
public sealed class ItemsController : ControllerBase
{
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

        return CreatedAtAction(
            nameof(GetByIdAsync),
            new { id = item.Id },
            new ItemCreatedDto(item.Id, item.InternalSKU, item.Name, item.CreatedAt));
    }

    [HttpGet("{id:int}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Items
            .AsNoTracking()
            .Include(i => i.Barcodes)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        return Ok(item);
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

    public sealed record ItemCreatedDto(int Id, string InternalSKU, string Name, DateTimeOffset CreatedAt);

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

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
