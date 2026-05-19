using LKvitai.MES.Modules.Warehouse.Application.Ports;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;

public sealed class AgnumNomenclatureImportService : IAgnumNomenclatureImportService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly IAgnumApiClientFactory _apiClientFactory;
    private readonly ILogger<AgnumNomenclatureImportService> _logger;

    public AgnumNomenclatureImportService(
        WarehouseDbContext dbContext,
        IAgnumApiClientFactory apiClientFactory,
        ILogger<AgnumNomenclatureImportService> logger)
    {
        _dbContext = dbContext;
        _apiClientFactory = apiClientFactory;
        _logger = logger;
    }

    public async Task<AgnumImportPreview> PreviewAsync(int sndId, CancellationToken ct = default)
    {
        var client = _apiClientFactory.GetForSndId(sndId);
        var products = await client.GetProductsAsync(ct);
        return await BuildPreviewAsync(sndId, products, ct);
    }

    private async Task<AgnumImportPreview> BuildPreviewAsync(
        int sndId,
        IReadOnlyList<AgnumProductDto> products,
        CancellationToken ct)
    {
        var existingLinks = await _dbContext.AgnumProductLinks
            .Where(x => x.SndId == sndId)
            .ToListAsync(ct);

        var itemsBySku = await _dbContext.Items
            .AsNoTracking()
            .ToListAsync(ct);

        var itemLookup = itemsBySku
            .ToDictionary(x => x.InternalSKU, StringComparer.OrdinalIgnoreCase);

        var linkByProductId = existingLinks
            .ToDictionary(x => x.AgnumProductId, x => x);

        var duplicateAgnumCodes = products
            .Select(x => NormalizeSkuCode(x.Code))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .GroupBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var preview = new AgnumImportPreview
        {
            SndId = sndId,
            TotalProducts = products.Count
        };

        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.Pcs))
            {
                preview.Conflicts.Add(new AgnumImportConflict
                {
                    AgnumProductId = product.Id,
                    Code = product.Code,
                    Reason = "MissingUoM"
                });
                continue;
            }

            if (duplicateAgnumCodes.Contains(NormalizeSkuCode(product.Code)))
            {
                preview.Conflicts.Add(new AgnumImportConflict
                {
                    AgnumProductId = product.Id,
                    Code = product.Code,
                    Reason = "DuplicateAgnumCode"
                });
                continue;
            }

            if (linkByProductId.TryGetValue(product.Id, out var existingLink))
            {
                if (itemLookup.TryGetValue(product.Code, out var conflictingItem) && conflictingItem.Id != existingLink.ItemId)
                {
                    preview.Conflicts.Add(new AgnumImportConflict
                    {
                        AgnumProductId = product.Id,
                        Code = product.Code,
                        Reason = "LinkedToDifferentItem"
                    });
                }
                else
                {
                    preview.ToUpdate.Add(new AgnumImportCandidate
                    {
                        AgnumProductId = product.Id,
                        Code = product.Code,
                        Name = product.Name,
                        Pcs = product.Pcs
                    });
                }

                continue;
            }

            if (itemLookup.ContainsKey(product.Code))
            {
                preview.Conflicts.Add(new AgnumImportConflict
                {
                    AgnumProductId = product.Id,
                    Code = product.Code,
                    Reason = "DuplicateSku"
                });
                continue;
            }

            preview.ToCreate.Add(new AgnumImportCandidate
            {
                AgnumProductId = product.Id,
                Code = product.Code,
                Name = product.Name,
                Pcs = product.Pcs
            });
        }

        return preview;
    }

    public async Task<AgnumImportResult> ApplyAsync(int sndId, CancellationToken ct = default)
    {
        var products = await _apiClientFactory.GetForSndId(sndId).GetProductsAsync(ct);
        var preview = await BuildPreviewAsync(sndId, products, ct);
        var productById = products.ToDictionary(x => x.Id);
        var existingLinks = await _dbContext.AgnumProductLinks
            .Where(x => x.SndId == sndId)
            .ToDictionaryAsync(x => x.AgnumProductId, x => x, ct);

        var created = 0;
        var updated = 0;

        foreach (var candidate in preview.ToCreate)
        {
            if (!productById.TryGetValue(candidate.AgnumProductId, out var product))
            {
                continue;
            }

            await CreateProductAsync(product, sndId, ct);
            created++;
        }

        foreach (var candidate in preview.ToUpdate)
        {
            if (!productById.TryGetValue(candidate.AgnumProductId, out var product))
            {
                continue;
            }

            if (!existingLinks.TryGetValue(product.Id, out var link))
            {
                continue;
            }

            await UpdateProductAsync(link, product, sndId, ct);
            updated++;
        }

        await _dbContext.SaveChangesAsync(ct);

        return new AgnumImportResult
        {
            Created = created,
            Updated = updated,
            Skipped = preview.Conflicts.Count,
            Conflicts = preview.Conflicts
        };
    }

    private async Task CreateProductAsync(AgnumProductDto product, int sndId, CancellationToken ct)
    {
        var category = await EnsureCategoryHierarchyAsync(product.Group, product.Category, product.Subgroup, ct);
        var unitOfMeasure = await EnsureUnitOfMeasureAsync(product.Pcs, product.UnitOfMeasureType, ct);
        var supplier = await EnsureSupplierAsync(product.SupplierCode, product.SupplierName, ct);
        var item = new Item
        {
            InternalSKU = product.Code,
            Name = product.Name,
            BaseUoM = unitOfMeasure.Code,
            BaseUnit = unitOfMeasure,
            Status = product.Enabled ? "Active" : "Discontinued",
            Weight = NormalizePositiveDecimal(product.Netto),
            Category = category,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            PrimaryBarcode = GetProductBarcodes(product).FirstOrDefault()
        };

        _dbContext.Items.Add(item);
        await AddItemBarcodesAsync(item, product, ct);
        await EnsureSupplierItemMappingAsync(item, supplier, product, ct);

        _dbContext.AgnumProductLinks.Add(new AgnumProductLink
        {
            Item = item,
            SndId = sndId,
            AgnumProductId = product.Id,
            AgnumCode = product.Code,
            AgnumEnabled = product.Enabled,
            AgnumModifiedAt = product.ModifyDate,
            LastImportedAt = DateTime.UtcNow,
            RawHash = ComputeRawHash(product)
        });

        await AddExternalAttributesAsync(item, product, sndId, ct);
    }

    private async Task UpdateProductAsync(AgnumProductLink link, AgnumProductDto product, int sndId, CancellationToken ct)
    {
        var item = await _dbContext.Items.FindAsync(new object[] { link.ItemId }, ct);
        if (item is null)
        {
            _logger.LogWarning("Missing item for Agnum product link {LinkId} during update.", link.Id);
            return;
        }

        var category = await EnsureCategoryHierarchyAsync(product.Group, product.Category, product.Subgroup, ct);
        var unitOfMeasure = await EnsureUnitOfMeasureAsync(product.Pcs, product.UnitOfMeasureType, ct);
        var supplier = await EnsureSupplierAsync(product.SupplierCode, product.SupplierName, ct);

        item.Name = product.Name;
        item.BaseUoM = unitOfMeasure.Code;
        item.BaseUnit = unitOfMeasure;
        item.Category = category;
        item.Status = product.Enabled ? "Active" : "Discontinued";
        item.Weight = NormalizePositiveDecimal(product.Netto);
        item.PrimaryBarcode = GetProductBarcodes(product).FirstOrDefault() ?? item.PrimaryBarcode;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await AddItemBarcodesAsync(item, product, ct);
        await EnsureSupplierItemMappingAsync(item, supplier, product, ct);
        await ReplaceExternalAttributesAsync(item, product, sndId, ct);

        link.AgnumCode = product.Code;
        link.AgnumEnabled = product.Enabled;
        link.AgnumModifiedAt = product.ModifyDate;
        link.LastImportedAt = DateTime.UtcNow;
        link.RawHash = ComputeRawHash(product);
    }

    private async Task AddItemBarcodesAsync(Item item, AgnumProductDto product, CancellationToken ct)
    {
        var distinctBarcodes = GetProductBarcodes(product);

        if (distinctBarcodes.Count == 0)
        {
            return;
        }

        var existingBarcodes = await _dbContext.ItemBarcodes
            .Where(x => distinctBarcodes.Contains(x.Barcode))
            .Select(x => x.Barcode)
            .ToListAsync(ct);

        foreach (var barcode in distinctBarcodes.Except(existingBarcodes, StringComparer.OrdinalIgnoreCase))
        {
            _dbContext.ItemBarcodes.Add(new ItemBarcode
            {
                Item = item,
                Barcode = barcode,
                BarcodeType = "Other",
                IsPrimary = false
            });
        }
    }

    private async Task AddExternalAttributesAsync(Item item, AgnumProductDto product, int sndId, CancellationToken ct)
    {
        var attributes = BuildAttributes(product, sndId);
        foreach (var attribute in attributes)
        {
            attribute.Item = item;
            _dbContext.ItemExternalAttributes.Add(attribute);
        }

        await Task.CompletedTask;
    }

    private async Task ReplaceExternalAttributesAsync(Item item, AgnumProductDto product, int sndId, CancellationToken ct)
    {
        var sourceContext = sndId.ToString();
        var existingAttributes = _dbContext.ItemExternalAttributes
            .Where(x => x.ItemId == item.Id && x.SourceSystem == "AGNUM" && x.SourceContext == sourceContext);

        _dbContext.ItemExternalAttributes.RemoveRange(existingAttributes);
        await AddExternalAttributesAsync(item, product, sndId, ct);
    }

    private IEnumerable<ItemExternalAttribute> BuildAttributes(AgnumProductDto product, int sndId)
    {
        var sourceContext = sndId.ToString();
        var attributes = new List<ItemExternalAttribute?>
        {
            CreateAttribute(sourceContext, "group", product.Group),
            CreateAttribute(sourceContext, "category", product.Category),
            CreateAttribute(sourceContext, "subgroup", product.Subgroup),
            CreateAttribute(sourceContext, "direction", product.Direction),
            CreateAttribute(sourceContext, "branch", product.Branch),
            CreateAttribute(sourceContext, "place", product.Place),
            CreateAttribute(sourceContext, "f1", product.F1),
            CreateAttribute(sourceContext, "f2", product.F2),
            CreateAttribute(sourceContext, "supplierCode", product.SupplierCode),
            CreateAttribute(sourceContext, "supplierName", product.SupplierName),
            CreateAttribute(sourceContext, "supplierSku", product.SupplierSku),
            CreateAttribute(sourceContext, "uomType", product.UnitOfMeasureType)
        };

        return attributes.Where(x => x is not null)!;
    }

    private static ItemExternalAttribute? CreateAttribute(string sourceContext, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var attribute = new ItemExternalAttribute
        {
            SourceContext = sourceContext,
            Key = key,
            ValueText = value.Trim()
        };

        if (decimal.TryParse(value.Trim(), out var numericValue))
        {
            attribute.ValueNumber = numericValue;
            attribute.ValueText = null;
        }

        return attribute;
    }

    private async Task<ItemCategory> EnsureCategoryHierarchyAsync(string? group, string? category, string? subgroup, CancellationToken ct)
    {
        ItemCategory? parent = null;

        if (!string.IsNullOrWhiteSpace(group))
        {
            parent = await GetOrCreateCategoryAsync(group.Trim(), null, ct);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            parent = await GetOrCreateCategoryAsync(category.Trim(), parent, ct);
        }

        if (!string.IsNullOrWhiteSpace(subgroup))
        {
            parent = await GetOrCreateCategoryAsync(subgroup.Trim(), parent, ct);
        }

        if (parent is not null)
        {
            return parent;
        }

        return await GetOrCreateCategoryAsync("Agnum", null, ct);
    }

    private async Task<ItemCategory> GetOrCreateCategoryAsync(string name, ItemCategory? parentCategory, CancellationToken ct)
    {
        var normalizedCode = NormalizeCategoryCode(name);
        var normalizedCodeUpper = normalizedCode.ToUpperInvariant();
        if (parentCategory is not null)
        {
            normalizedCode = NormalizeCategoryCode(parentCategory.Code + "-" + name);
            normalizedCodeUpper = normalizedCode.ToUpperInvariant();
        }

        var tracked = _dbContext.ItemCategories.Local
            .FirstOrDefault(x => string.Equals(x.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (tracked is not null)
        {
            return tracked;
        }

        var existing = await _dbContext.ItemCategories
            .FirstOrDefaultAsync(x => x.Code.ToUpper() == normalizedCodeUpper, ct);

        if (existing is not null)
        {
            return existing;
        }

        var category = new ItemCategory
        {
            Code = normalizedCode,
            Name = name,
            ParentCategory = parentCategory
        };

        _dbContext.ItemCategories.Add(category);
        return category;
    }

    private async Task<UnitOfMeasure> EnsureUnitOfMeasureAsync(string code, string? type, CancellationToken ct)
    {
        var normalizedCode = NormalizeUnitOfMeasureCode(code);
        var normalizedCodeUpper = normalizedCode.ToUpperInvariant();

        var tracked = _dbContext.UnitOfMeasures.Local
            .FirstOrDefault(x => string.Equals(x.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (tracked is not null)
        {
            return tracked;
        }

        var existing = await _dbContext.UnitOfMeasures
            .FirstOrDefaultAsync(x => x.Code.ToUpper() == normalizedCodeUpper, ct);
        if (existing is not null)
        {
            return existing;
        }

        var unitOfMeasure = new UnitOfMeasure
        {
            Code = normalizedCode,
            Name = normalizedCode,
            Type = NormalizeUnitOfMeasureType(type, normalizedCode)
        };

        _dbContext.UnitOfMeasures.Add(unitOfMeasure);
        return unitOfMeasure;
    }

    private async Task<Supplier?> EnsureSupplierAsync(string? code, string? name, CancellationToken ct)
    {
        var normalizedCode = NormalizeSupplierCode(code);
        if (normalizedCode is null)
        {
            normalizedCode = NormalizeSupplierCode(name);
        }

        if (normalizedCode is null)
        {
            return null;
        }

        var supplierName = string.IsNullOrWhiteSpace(name) ? normalizedCode : name.Trim();
        var normalizedCodeUpper = normalizedCode.ToUpperInvariant();
        var tracked = _dbContext.Suppliers.Local
            .FirstOrDefault(x => string.Equals(x.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
        if (tracked is not null)
        {
            if (string.IsNullOrWhiteSpace(tracked.Name) || tracked.Name == tracked.Code)
            {
                tracked.Name = supplierName;
            }

            return tracked;
        }

        var existing = await _dbContext.Suppliers
            .FirstOrDefaultAsync(x => x.Code.ToUpper() == normalizedCodeUpper, ct);
        if (existing is not null)
        {
            if (string.IsNullOrWhiteSpace(existing.Name) || existing.Name == existing.Code)
            {
                existing.Name = supplierName;
            }

            return existing;
        }

        var supplier = new Supplier
        {
            Code = normalizedCode,
            Name = supplierName,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Suppliers.Add(supplier);
        return supplier;
    }

    private async Task EnsureSupplierItemMappingAsync(Item item, Supplier? supplier, AgnumProductDto product, CancellationToken ct)
    {
        if (supplier is null)
        {
            return;
        }

        var supplierSku = (product.SupplierSku ?? product.F2 ?? product.Code).Trim();
        if (string.IsNullOrWhiteSpace(supplierSku))
        {
            return;
        }

        var tracked = _dbContext.SupplierItemMappings.Local.FirstOrDefault(x =>
            string.Equals(x.SupplierSKU, supplierSku, StringComparison.OrdinalIgnoreCase)
            && (ReferenceEquals(x.Supplier, supplier) || x.SupplierId != 0 && x.SupplierId == supplier.Id));

        if (tracked is not null)
        {
            if (tracked.Item is null && tracked.ItemId == 0)
            {
                tracked.Item = item;
            }

            return;
        }

        if (supplier.Id != 0)
        {
            var existing = await _dbContext.SupplierItemMappings
                .FirstOrDefaultAsync(x => x.SupplierId == supplier.Id && x.SupplierSKU == supplierSku, ct);
            if (existing is not null)
            {
                if (existing.ItemId == item.Id)
                {
                    return;
                }

                _logger.LogWarning(
                    "Supplier SKU {SupplierSku} for supplier {SupplierCode} is already mapped to item {ItemId}.",
                    supplierSku,
                    supplier.Code,
                    existing.ItemId);
                return;
            }
        }

        _dbContext.SupplierItemMappings.Add(new SupplierItemMapping
        {
            Supplier = supplier,
            Item = item,
            SupplierSKU = supplierSku
        });
    }

    private static string NormalizeCategoryCode(string value)
    {
        var normalized = value.Trim().ToUpperInvariant();
        var builder = new System.Text.StringBuilder();

        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '_')
            {
                builder.Append(c);
            }
            else if (char.IsWhiteSpace(c))
            {
                builder.Append('-');
            }
        }

        var code = builder.ToString();
        if (code.Length > 50)
        {
            code = code[..50];
        }

        return code;
    }

    private static string NormalizeUnitOfMeasureCode(string value)
    {
        var code = value.Trim();
        return code.Length > 10 ? code[..10] : code;
    }

    private static string NormalizeUnitOfMeasureType(string? type, string code)
    {
        if (!string.IsNullOrWhiteSpace(type))
        {
            var normalizedType = type.Trim();
            if (normalizedType.Equals("Weight", StringComparison.OrdinalIgnoreCase)
                || normalizedType.Equals("Volume", StringComparison.OrdinalIgnoreCase)
                || normalizedType.Equals("Piece", StringComparison.OrdinalIgnoreCase)
                || normalizedType.Equals("Length", StringComparison.OrdinalIgnoreCase))
            {
                return char.ToUpperInvariant(normalizedType[0]) + normalizedType[1..].ToLowerInvariant();
            }
        }

        var normalizedCode = code.Trim().ToLowerInvariant();
        return normalizedCode switch
        {
            "kg" or "g" or "gr" or "t" => "Weight",
            "l" or "ltr" or "m3" => "Volume",
            "m" or "m2" or "cm" or "mm" => "Length",
            _ => "Piece"
        };
    }

    private static string? NormalizeSupplierCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = NormalizeCategoryCode(value);
        return string.IsNullOrWhiteSpace(normalized) ? null : normalized;
    }

    private static decimal? NormalizePositiveDecimal(decimal? value)
    {
        return value > 0 ? value : null;
    }

    private static string NormalizeSkuCode(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static List<string> GetProductBarcodes(AgnumProductDto product)
    {
        var barcodes = new List<string>();
        if (product.Barcodes is not null)
        {
            barcodes.AddRange(product.Barcodes);
        }

        if (!string.IsNullOrWhiteSpace(product.Barcode))
        {
            barcodes.Add(product.Barcode);
        }

        return barcodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string ComputeRawHash(AgnumProductDto product)
    {
        var text = string.Join("|",
            product.Id,
            product.Code ?? string.Empty,
            product.Name ?? string.Empty,
            product.Enabled,
            product.Pcs ?? string.Empty,
            product.Netto?.ToString("G29") ?? string.Empty,
            product.Brutto?.ToString("G29") ?? string.Empty,
            string.Join(";", product.Barcodes ?? Enumerable.Empty<string>()),
            product.Group ?? string.Empty,
            product.Category ?? string.Empty,
            product.Subgroup ?? string.Empty,
            product.Direction ?? string.Empty,
            product.Branch ?? string.Empty,
            product.Place ?? string.Empty,
            product.F1 ?? string.Empty,
            product.F2 ?? string.Empty,
            product.SupplierCode ?? string.Empty,
            product.SupplierName ?? string.Empty,
            product.SupplierSku ?? string.Empty,
            product.UnitOfMeasureType ?? string.Empty);

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
