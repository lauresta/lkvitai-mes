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

        var validUoms = await _dbContext.UnitOfMeasures
            .Select(x => x.Code)
            .ToListAsync(ct);

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

        var preview = new AgnumImportPreview
        {
            SndId = sndId,
            TotalProducts = products.Count
        };

        foreach (var product in products)
        {
            if (string.IsNullOrWhiteSpace(product.Pcs) || !validUoms.Contains(product.Pcs, StringComparer.OrdinalIgnoreCase))
            {
                preview.Conflicts.Add(new AgnumImportConflict
                {
                    AgnumProductId = product.Id,
                    Code = product.Code,
                    Reason = "UnknownUoM"
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
        var preview = await PreviewAsync(sndId, ct);
        var products = await _apiClientFactory.GetForSndId(sndId).GetProductsAsync(ct);
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
        var categoryId = await EnsureCategoryHierarchyAsync(product.Group, product.Category, product.Subgroup, ct);
        var item = new Item
        {
            InternalSKU = product.Code,
            Name = product.Name,
            BaseUoM = product.Pcs,
            Status = product.Enabled ? "Active" : "Discontinued",
            Weight = product.Netto,
            CategoryId = categoryId,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            PrimaryBarcode = product.Barcodes?.FirstOrDefault()
        };

        _dbContext.Items.Add(item);
        await AddItemBarcodesAsync(item, product, ct);

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

        item.Name = product.Name;
        item.BaseUoM = product.Pcs;
        item.Status = product.Enabled ? "Active" : "Discontinued";
        item.Weight = product.Netto;
        item.PrimaryBarcode = product.Barcodes?.FirstOrDefault() ?? item.PrimaryBarcode;
        item.UpdatedAt = DateTimeOffset.UtcNow;

        await AddItemBarcodesAsync(item, product, ct);
        await ReplaceExternalAttributesAsync(item, product, sndId, ct);

        link.AgnumCode = product.Code;
        link.AgnumEnabled = product.Enabled;
        link.AgnumModifiedAt = product.ModifyDate;
        link.LastImportedAt = DateTime.UtcNow;
        link.RawHash = ComputeRawHash(product);
    }

    private async Task AddItemBarcodesAsync(Item item, AgnumProductDto product, CancellationToken ct)
    {
        var barcodes = product.Barcodes ?? new List<string>();
        var distinctBarcodes = barcodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

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
            CreateAttribute(sourceContext, "f2", product.F2)
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

    private async Task<int> EnsureCategoryHierarchyAsync(string? group, string? category, string? subgroup, CancellationToken ct)
    {
        int? parentId = null;

        if (!string.IsNullOrWhiteSpace(group))
        {
            parentId = await GetOrCreateCategoryAsync(group.Trim(), null, ct);
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            parentId = await GetOrCreateCategoryAsync(category.Trim(), parentId, ct);
        }

        if (!string.IsNullOrWhiteSpace(subgroup))
        {
            parentId = await GetOrCreateCategoryAsync(subgroup.Trim(), parentId, ct);
        }

        if (parentId.HasValue)
        {
            return parentId.Value;
        }

        return await GetOrCreateCategoryAsync("Agnum", null, ct);
    }

    private async Task<int> GetOrCreateCategoryAsync(string name, int? parentCategoryId, CancellationToken ct)
    {
        var normalizedCode = NormalizeCategoryCode(name);
        if (parentCategoryId.HasValue)
        {
            var parent = await _dbContext.ItemCategories.FindAsync(new object[] { parentCategoryId.Value }, ct);
            if (parent is null)
            {
                parentCategoryId = null;
            }
            else
            {
                normalizedCode = NormalizeCategoryCode(parent.Code + "-" + name);
            }
        }

        var existing = await _dbContext.ItemCategories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Code == normalizedCode, ct);

        if (existing is not null)
        {
            return existing.Id;
        }

        var category = new ItemCategory
        {
            Code = normalizedCode,
            Name = name,
            ParentCategoryId = parentCategoryId
        };

        _dbContext.ItemCategories.Add(category);
        await _dbContext.SaveChangesAsync(ct);
        return category.Id;
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
            product.F2 ?? string.Empty);

        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(text);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}
