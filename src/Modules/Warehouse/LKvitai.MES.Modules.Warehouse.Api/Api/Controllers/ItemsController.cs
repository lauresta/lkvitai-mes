using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.BuildingBlocks.SharedKernel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Npgsql;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/items")]
public sealed class ItemsController : ControllerBase
{
    private const string GetItemByIdRouteName = "GetItemById";

    private readonly WarehouseDbContext _dbContext;
    private readonly ISkuGenerationService _skuGenerationService;
    private readonly IItemPhotoService _itemPhotoService;
    private readonly IItemImageSearchCapabilityService _searchCapabilityService;
    private readonly IItemPriceHistoryService _priceHistoryService;
    private readonly ICurrentUserService _currentUserService;

    public ItemsController(
        WarehouseDbContext dbContext,
        ISkuGenerationService skuGenerationService,
        IItemPhotoService itemPhotoService,
        IItemImageSearchCapabilityService searchCapabilityService,
        IItemPriceHistoryService priceHistoryService,
        ICurrentUserService currentUserService)
    {
        _dbContext = dbContext;
        _skuGenerationService = skuGenerationService;
        _itemPhotoService = itemPhotoService;
        _searchCapabilityService = searchCapabilityService;
        _priceHistoryService = priceHistoryService;
        _currentUserService = currentUserService;
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
                i.Name.ToLower().Contains(normalized) ||
                (i.Tags != null && i.Tags.ToLower().Contains(normalized.TrimStart('#'))));
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
                i.Weight,
                i.Volume,
                i.BasePrice,
                i.PurchasePrice,
                i.ProductConfigId,
                SplitTags(i.Tags),
                i.CreatedAt,
                i.UpdatedAt,
                null,
                null,
                i.ItemType.ToString(),
                i.CostType))
            .ToListAsync(cancellationToken);

        if (items.Count > 0)
        {
            var itemIds = items.Select(x => x.Id).ToList();
            var primaryPhotos = await _dbContext.ItemPhotos
                .AsNoTracking()
                .Where(x => itemIds.Contains(x.ItemId) && x.IsPrimary)
                .Select(x => new { x.ItemId, x.Id })
                .ToListAsync(cancellationToken);

            var byItemId = primaryPhotos.ToDictionary(x => x.ItemId, x => x.Id);
            for (var i = 0; i < items.Count; i++)
            {
                var row = items[i];
                if (!byItemId.TryGetValue(row.Id, out var photoId))
                {
                    continue;
                }

                items[i] = row with
                {
                    PrimaryPhotoId = photoId,
                    PrimaryThumbnailUrl = ItemPhotoService.BuildProxyUrl(row.Id, photoId, "thumb")
                };
            }
        }

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

        if (!TryParseItemType(request.ItemType, out var itemType))
        {
            return ValidationFailure($"Invalid itemType '{request.ItemType}'. Expected 'Stock' or 'Service'.");
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
            ProductConfigId = request.ProductConfigId?.Trim(),
            Tags = SerializeTags(request.Tags),
            BasePrice = request.BasePrice,
            PurchasePrice = request.PurchasePrice,
            ItemType = itemType,
            CostType = itemType == ItemType.Service && !string.IsNullOrWhiteSpace(request.CostType)
                ? request.CostType.Trim()
                : null
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
        await WritePriceHistoryIfSetAsync(item.Id, ItemPriceTypes.Base, null, request.BasePrice, cancellationToken);
        await WritePriceHistoryIfSetAsync(item.Id, ItemPriceTypes.Purchase, null, request.PurchasePrice, cancellationToken);

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
        var cached = await Cache.GetAsync<ItemDetailDto>(cacheKey, cancellationToken);
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

        var photos = await _itemPhotoService.ListAsync(id, cancellationToken);
        var primaryPhoto = photos.FirstOrDefault(x => x.IsPrimary);

        var dto = new ItemDetailDto(
            item.Id,
            item.InternalSKU,
            item.Name,
            item.Description,
            item.CategoryId,
            item.BaseUoM,
            item.Weight,
            item.Volume,
            item.BasePrice,
            item.PurchasePrice,
            item.RequiresLotTracking,
            item.RequiresQC,
            item.Status,
            item.PrimaryBarcode,
            item.ProductConfigId,
            SplitTags(item.Tags),
            item.CreatedAt,
            item.UpdatedAt,
            primaryPhoto?.ThumbUrl,
            primaryPhoto?.Id,
            photos,
            item.ItemType.ToString(),
            item.CostType);

        await Cache.SetAsync(cacheKey, dto, TimeSpan.FromHours(1), cancellationToken);
        return Ok(dto);
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

        if (!TryParseItemType(request.ItemType, out var itemType))
        {
            return ValidationFailure($"Invalid itemType '{request.ItemType}'. Expected 'Stock' or 'Service'.");
        }

        var oldBasePrice = item.BasePrice;
        var oldPurchasePrice = item.PurchasePrice;

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
        item.Tags = SerializeTags(request.Tags);
        item.BasePrice = request.BasePrice;
        item.PurchasePrice = request.PurchasePrice;
        item.ItemType = itemType;
        item.CostType = itemType == ItemType.Service && !string.IsNullOrWhiteSpace(request.CostType)
            ? request.CostType.Trim()
            : null;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"item:{id}", cancellationToken);

        if (oldBasePrice != request.BasePrice)
        {
            await WritePriceHistoryIfChangedAsync(id, ItemPriceTypes.Base, oldBasePrice, request.BasePrice, cancellationToken);
        }

        if (oldPurchasePrice != request.PurchasePrice)
        {
            await WritePriceHistoryIfChangedAsync(id, ItemPriceTypes.Purchase, oldPurchasePrice, request.PurchasePrice, cancellationToken);
        }

        return Ok(new ItemUpdatedDto(item.Id, item.InternalSKU, item.Name, item.Status, item.UpdatedAt));
    }

    [HttpGet("{id:int}/price-overrides")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetPriceOverridesAsync(int id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        var overridesByGroup = await _dbContext.ItemPriceOverrides
            .AsNoTracking()
            .Where(x => x.ItemId == id)
            .ToDictionaryAsync(x => x.PriceGroupId, x => x.Amount, cancellationToken);

        var groups = await _dbContext.PriceGroups
            .AsNoTracking()
            .Where(x => x.IsActive)
            .OrderBy(x => x.Code)
            .Select(x => new ItemPriceOverrideDto(
                x.Id,
                x.Code,
                x.Name,
                overridesByGroup.ContainsKey(x.Id) ? overridesByGroup[x.Id] : null,
                item.BasePrice))
            .ToListAsync(cancellationToken);

        return Ok(groups);
    }

    [HttpPut("{id:int}/price-overrides/{priceGroupId:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> SetPriceOverrideAsync(
        int id,
        int priceGroupId,
        [FromBody] SetPriceOverrideRequest request,
        CancellationToken cancellationToken = default)
    {
        var itemExists = await _dbContext.Items.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken);
        if (!itemExists)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        var groupExists = await _dbContext.PriceGroups.AsNoTracking().AnyAsync(x => x.Id == priceGroupId, cancellationToken);
        if (!groupExists)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Price group '{priceGroupId}' does not exist."));
        }

        if (request.Amount is < 0)
        {
            return ValidationFailure("Amount must be greater than or equal to zero.");
        }

        var existing = await _dbContext.ItemPriceOverrides
            .FirstOrDefaultAsync(x => x.ItemId == id && x.PriceGroupId == priceGroupId, cancellationToken);
        var oldAmount = existing?.Amount;

        if (request.Amount is null)
        {
            if (existing is not null)
            {
                _dbContext.ItemPriceOverrides.Remove(existing);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        else if (existing is null)
        {
            _dbContext.ItemPriceOverrides.Add(new ItemPriceOverride
            {
                ItemId = id,
                PriceGroupId = priceGroupId,
                Amount = request.Amount.Value
            });
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        else
        {
            existing.Amount = request.Amount.Value;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        if (oldAmount != request.Amount)
        {
            await _priceHistoryService.WriteAsync(new ItemPriceHistoryWriteRequest(
                id,
                ItemPriceTypes.GroupOverride,
                priceGroupId,
                oldAmount,
                request.Amount ?? 0m,
                _currentUserService.GetCurrentUserId(),
                DateTimeOffset.UtcNow,
                request.Amount is null ? "Override removed, reverted to base price" : null), cancellationToken);
        }

        await Cache.RemoveAsync($"item:{id}", cancellationToken);
        return Ok();
    }

    [HttpGet("{id:int}/price-history")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetPriceHistoryAsync(int id, CancellationToken cancellationToken = default)
    {
        var itemExists = await _dbContext.Items.AsNoTracking().AnyAsync(x => x.Id == id, cancellationToken);
        if (!itemExists)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        var rows = await _priceHistoryService.QueryAsync(id, cancellationToken);
        return Ok(rows);
    }

    [HttpGet("tags")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetTagsAsync(
        [FromQuery] string? search = null,
        [FromQuery] int limit = 30,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var normalizedSearch = NormalizeTag(search);
        var values = await _dbContext.Items
            .AsNoTracking()
            .Where(x => x.Tags != null && x.Tags != "")
            .Select(x => x.Tags!)
            .ToListAsync(cancellationToken);

        var tags = values
            .SelectMany(SplitTags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(x => string.IsNullOrWhiteSpace(normalizedSearch) ||
                        x.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();

        return Ok(new ItemTagsResponse(tags));
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

    [HttpPost("{id:int}/photos")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> UploadPhotoAsync(
        int id,
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length <= 0)
        {
            return ValidationFailure("Photo file is required.");
        }

        var availability = await _itemPhotoService.EnsureImagesAvailableAsync(cancellationToken);
        if (!availability.IsAvailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Item image storage unavailable",
                Detail = availability.Reason ?? "Item image storage is unavailable.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }

        try
        {
            await using var input = file.OpenReadStream();
            var photo = await _itemPhotoService.UploadAsync(
                id,
                file.FileName,
                file.ContentType,
                input,
                file.Length,
                cancellationToken);

            await Cache.RemoveAsync($"item:{id}", cancellationToken);
            return Ok(photo);
        }
        catch (KeyNotFoundException)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }
        catch (InvalidOperationException ex)
        {
            return ValidationFailure(ex.Message);
        }
    }

    [HttpGet("{id:int}/photos")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> ListPhotosAsync(int id, CancellationToken cancellationToken = default)
    {
        var itemExists = await _dbContext.Items
            .AsNoTracking()
            .AnyAsync(x => x.Id == id, cancellationToken);
        if (!itemExists)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Item with ID {id} does not exist."));
        }

        var photos = await _itemPhotoService.ListAsync(id, cancellationToken);
        return Ok(new ItemPhotosResponse(id, photos));
    }

    [HttpGet("{id:int}/photos/{photoId:guid}")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetPhotoAsync(
        int id,
        Guid photoId,
        [FromQuery] string size = "thumb",
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(size, "thumb", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(size, "original", StringComparison.OrdinalIgnoreCase))
        {
            return ValidationFailure("Query parameter 'size' must be 'thumb' or 'original'.");
        }

        var photo = await _itemPhotoService.GetPhotoAsync(id, photoId, size, cancellationToken);
        if (photo is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Photo '{photoId}' not found for item '{id}'."));
        }

        var normalizedEtag = photo.ETag.Trim('"');
        Response.Headers[HeaderNames.CacheControl] = $"public, max-age={_itemPhotoService.CacheMaxAgeSeconds}";
        Response.Headers[HeaderNames.ETag] = $"\"{normalizedEtag}\"";

        if (Request.Headers.TryGetValue(HeaderNames.IfNoneMatch, out var ifNoneMatchValues))
        {
            var matches = ifNoneMatchValues.Any(x =>
                string.Equals(x?.Trim('"'), normalizedEtag, StringComparison.OrdinalIgnoreCase));
            if (matches)
            {
                return StatusCode(StatusCodes.Status304NotModified);
            }
        }

        return File(photo.Stream, photo.ContentType);
    }

    [HttpPost("{id:int}/photos/{photoId:guid}/make-primary")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> MakePrimaryPhotoAsync(int id, Guid photoId, CancellationToken cancellationToken = default)
    {
        var updated = await _itemPhotoService.MakePrimaryAsync(id, photoId, cancellationToken);
        if (!updated)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Photo '{photoId}' not found for item '{id}'."));
        }

        await Cache.RemoveAsync($"item:{id}", cancellationToken);
        var photos = await _itemPhotoService.ListAsync(id, cancellationToken);
        return Ok(new ItemPhotosResponse(id, photos));
    }

    [HttpDelete("{id:int}/photos/{photoId:guid}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> DeletePhotoAsync(int id, Guid photoId, CancellationToken cancellationToken = default)
    {
        var deleted = await _itemPhotoService.DeleteAsync(id, photoId, cancellationToken);
        if (!deleted)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Photo '{photoId}' not found for item '{id}'."));
        }

        await Cache.RemoveAsync($"item:{id}", cancellationToken);
        var photos = await _itemPhotoService.ListAsync(id, cancellationToken);
        return Ok(new ItemPhotosResponse(id, photos));
    }

    [HttpPost("{id:int}/photos/recompute-embeddings")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> RecomputeEmbeddingsAsync(int id, CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _itemPhotoService.RecomputeEmbeddingsAsync(id, cancellationToken);
            return Ok(new { UpdatedCount = count });
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Image search capability unavailable",
                Detail = ex.Message,
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
    }

    [HttpPost("search-by-image")]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<IActionResult> SearchByImageAsync(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length <= 0)
        {
            return ValidationFailure("Photo file is required.");
        }

        var capability = await _searchCapabilityService.GetCapabilityAsync(cancellationToken);
        if (!capability.IsEnabled)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Image similarity search unavailable",
                Detail = capability.DisabledReason ?? "Image similarity search is unavailable.",
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }

        try
        {
            await using var stream = file.OpenReadStream();
            var results = await _itemPhotoService.SearchByImageAsync(stream, cancellationToken);
            return Ok(new ImageSearchResponse(results));
        }
        catch (InvalidOperationException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new ProblemDetails
            {
                Title = "Image similarity search unavailable",
                Detail = ex.Message,
                Status = StatusCodes.Status503ServiceUnavailable
            });
        }
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

    private static IReadOnlyList<string> NormalizeTags(IEnumerable<string>? tags)
    {
        if (tags is null)
        {
            return Array.Empty<string>();
        }

        return tags
            .SelectMany(SplitTagInput)
            .Select(NormalizeTag)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string? SerializeTags(IEnumerable<string>? tags)
    {
        var normalized = NormalizeTags(tags);
        return normalized.Count == 0 ? null : string.Join(",", normalized);
    }

    private static IReadOnlyList<string> SplitTags(string? tags)
        => NormalizeTags(SplitTagInput(tags));

    private static IEnumerable<string> SplitTagInput(string? tags)
    {
        if (string.IsNullOrWhiteSpace(tags))
        {
            return Array.Empty<string>();
        }

        return tags.Split([',', ';', '|', ' ', '\t', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
    }

    private static string NormalizeTag(string? value)
        => value?.Trim().TrimStart('#').Trim() ?? string.Empty;

    private static bool TryParseItemType(string? value, out ItemType itemType)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            itemType = ItemType.Stock;
            return true;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out itemType);
    }

    private async Task WritePriceHistoryIfSetAsync(
        int itemId,
        string priceType,
        decimal? oldAmount,
        decimal? newAmount,
        CancellationToken cancellationToken)
    {
        if (newAmount is null)
        {
            return;
        }

        await _priceHistoryService.WriteAsync(new ItemPriceHistoryWriteRequest(
            itemId,
            priceType,
            null,
            oldAmount,
            newAmount.Value,
            _currentUserService.GetCurrentUserId(),
            DateTimeOffset.UtcNow,
            null), cancellationToken);
    }

    private async Task WritePriceHistoryIfChangedAsync(
        int itemId,
        string priceType,
        decimal? oldAmount,
        decimal? newAmount,
        CancellationToken cancellationToken)
    {
        await _priceHistoryService.WriteAsync(new ItemPriceHistoryWriteRequest(
            itemId,
            priceType,
            null,
            oldAmount,
            newAmount ?? 0m,
            _currentUserService.GetCurrentUserId(),
            DateTimeOffset.UtcNow,
            newAmount is null ? "Price cleared" : null), cancellationToken);
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
        string? ProductConfigId,
        IReadOnlyList<string>? Tags,
        decimal? BasePrice = null,
        decimal? PurchasePrice = null,
        string? ItemType = null,
        string? CostType = null);

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
        string? ProductConfigId,
        IReadOnlyList<string>? Tags,
        decimal? BasePrice = null,
        decimal? PurchasePrice = null,
        string? ItemType = null,
        string? CostType = null);

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
        decimal? Weight,
        decimal? Volume,
        decimal? BasePrice,
        decimal? PurchasePrice,
        string? ProductConfigId,
        IReadOnlyList<string> Tags,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        string? PrimaryThumbnailUrl,
        Guid? PrimaryPhotoId,
        string ItemType,
        string? CostType);

    public sealed record ItemDetailDto(
        int Id,
        string InternalSKU,
        string Name,
        string? Description,
        int CategoryId,
        string BaseUoM,
        decimal? Weight,
        decimal? Volume,
        decimal? BasePrice,
        decimal? PurchasePrice,
        bool RequiresLotTracking,
        bool RequiresQC,
        string Status,
        string? PrimaryBarcode,
        string? ProductConfigId,
        IReadOnlyList<string> Tags,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt,
        string? PrimaryThumbnailUrl,
        Guid? PrimaryPhotoId,
        IReadOnlyList<ItemPhotoDto> Photos,
        string ItemType,
        string? CostType);

    public sealed record AddBarcodeRequest(string Barcode, string BarcodeType, bool IsPrimary);
    public sealed record ItemBarcodeDto(int Id, string Barcode, string BarcodeType, bool IsPrimary);
    public sealed record ItemBarcodesResponse(int ItemId, string InternalSKU, IReadOnlyList<ItemBarcodeDto> Barcodes);
    public sealed record ItemPhotosResponse(int ItemId, IReadOnlyList<ItemPhotoDto> Photos);
    public sealed record ItemTagsResponse(IReadOnlyList<string> Tags);
    public sealed record ItemPriceOverrideDto(
        int PriceGroupId,
        string PriceGroupCode,
        string PriceGroupName,
        decimal? OverrideAmount,
        decimal? BasePrice);
    public sealed record SetPriceOverrideRequest(decimal? Amount);
    public sealed record ImageSearchResponse(IReadOnlyList<ItemImageSearchResultDto> Results);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
