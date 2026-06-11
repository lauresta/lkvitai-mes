using System.Diagnostics;
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
            var normalizedCode = NormalizeSkuCode(product.Code);
            if (string.IsNullOrWhiteSpace(normalizedCode))
            {
                preview.Conflicts.Add(new AgnumImportConflict
                {
                    AgnumProductId = product.Id,
                    Code = product.Code,
                    Reason = "MissingSku"
                });
                continue;
            }

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

            if (duplicateAgnumCodes.Contains(normalizedCode))
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

            if (itemLookup.TryGetValue(product.Code, out var existingItem))
            {
                preview.ToCreate.Add(new AgnumImportCandidate
                {
                    AgnumProductId = product.Id,
                    ExistingItemId = existingItem.Id,
                    Code = product.Code,
                    Name = product.Name,
                    Pcs = product.Pcs
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

    public async Task<AgnumImportResult> ApplyAsync(int sndId, CancellationToken ct = default, bool importPartners = true)
    {
        var client = _apiClientFactory.GetForSndId(sndId);
        var products = await client.GetProductsAsync(ct);
        var clients = importPartners
            ? await client.GetClientsAsync(ct)
            : Array.Empty<AgnumClientDto>();
        var preview = await BuildPreviewAsync(sndId, products, ct);
        var productById = products.ToDictionary(x => x.Id);
        var existingLinks = await _dbContext.AgnumProductLinks
            .Where(x => x.SndId == sndId)
            .ToDictionaryAsync(x => x.AgnumProductId, x => x, ct);

        var created = 0;
        var updated = 0;

        var autoDetectChanges = _dbContext.ChangeTracker.AutoDetectChangesEnabled;
        _dbContext.ChangeTracker.AutoDetectChangesEnabled = false;
        try
        {
            if (importPartners)
            {
                await EnsurePartnersAsync(clients, ct);
            }

            foreach (var candidate in preview.ToCreate)
            {
                if (!productById.TryGetValue(candidate.AgnumProductId, out var product))
                {
                    continue;
                }

                if (candidate.ExistingItemId is not null)
                {
                    await LinkExistingProductAsync(product, candidate.ExistingItemId.Value, sndId, ct);
                }
                else
                {
                    await CreateProductAsync(product, sndId, ct);
                }

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

            var saveStartedAt = Stopwatch.StartNew();
            _logger.LogInformation(
                "Saving Agnum nomenclature import for sndId {SndId}. Created={Created} Updated={Updated} Skipped={Skipped} TrackedEntities={TrackedEntities}",
                sndId,
                created,
                updated,
                preview.Conflicts.Count,
                _dbContext.ChangeTracker.Entries().Count());

            _dbContext.ChangeTracker.DetectChanges();
            await _dbContext.SaveChangesAsync(ct);
            _logger.LogInformation(
                "Saved Agnum nomenclature import for sndId {SndId} in {ElapsedMs} ms.",
                sndId,
                saveStartedAt.ElapsedMilliseconds);
        }
        finally
        {
            _dbContext.ChangeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }

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
            AgnumModifiedAt = NormalizeUtcDateTime(product.ModifyDate),
            LastImportedAt = DateTime.UtcNow,
            RawHash = ComputeRawHash(product)
        });

        await AddExternalAttributesAsync(item, product, sndId, ct);
    }

    private async Task LinkExistingProductAsync(AgnumProductDto product, int itemId, int sndId, CancellationToken ct)
    {
        var item = await _dbContext.Items.FindAsync(new object[] { itemId }, ct);
        if (item is null)
        {
            _logger.LogWarning(
                "Missing item {ItemId} for Agnum product {AgnumProductId} during link import.",
                itemId,
                product.Id);
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
        await AddExternalAttributesAsync(item, product, sndId, ct);

        _dbContext.AgnumProductLinks.Add(new AgnumProductLink
        {
            Item = item,
            SndId = sndId,
            AgnumProductId = product.Id,
            AgnumCode = product.Code,
            AgnumEnabled = product.Enabled,
            AgnumModifiedAt = NormalizeUtcDateTime(product.ModifyDate),
            LastImportedAt = DateTime.UtcNow,
            RawHash = ComputeRawHash(product)
        });
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
        link.AgnumModifiedAt = NormalizeUtcDateTime(product.ModifyDate);
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

    private async Task EnsurePartnersAsync(IReadOnlyList<AgnumClientDto> clients, CancellationToken ct)
    {
        await EnsureSuppliersAsync(clients.Where(IsSupplier).ToList(), ct);
        await EnsureCustomersAsync(clients.Where(IsBuyer).ToList(), ct);
    }

    private async Task EnsureSuppliersAsync(IReadOnlyList<AgnumClientDto> suppliers, CancellationToken ct)
    {
        if (suppliers.Count == 0)
        {
            return;
        }

        var normalizedCodes = suppliers
            .Select(x => NormalizeSupplierCode(x.Code))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var agnumClientIds = suppliers
            .Where(x => x.Id > 0)
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var normalizedCodeUpperSet = normalizedCodes
            .Select(x => x.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var existingSuppliers = await _dbContext.Suppliers
            .Where(x => normalizedCodeUpperSet.Contains(x.Code.ToUpper()) || x.AgnumClientId != null && agnumClientIds.Contains(x.AgnumClientId.Value))
            .ToListAsync(ct);
        var existingSuppliersByCode = existingSuppliers
            .ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);
        var existingSuppliersByAgnumId = existingSuppliers
            .Where(x => x.AgnumClientId is not null)
            .ToDictionary(x => x.AgnumClientId!.Value);

        var imported = 0;
        foreach (var supplierClient in suppliers)
        {
            var normalizedCode = NormalizeSupplierCode(supplierClient.Code);
            if (normalizedCode is null)
            {
                continue;
            }

            var supplierName = string.IsNullOrWhiteSpace(supplierClient.Name)
                ? normalizedCode
                : supplierClient.Name.Trim();

            var tracked = _dbContext.Suppliers.Local.FirstOrDefault(x =>
                x.AgnumClientId == supplierClient.Id
                || string.Equals(x.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
            if (tracked is not null)
            {
                tracked.AgnumClientId = supplierClient.Id;
                tracked.Name = supplierName;
                ApplyAgnumSupplierFields(tracked, supplierClient);
                tracked.UpdatedAt = DateTimeOffset.UtcNow;
                imported++;
                continue;
            }

            if ((supplierClient.Id > 0 && existingSuppliersByAgnumId.TryGetValue(supplierClient.Id, out var existing))
                || existingSuppliersByCode.TryGetValue(normalizedCode, out existing))
            {
                existing.AgnumClientId = supplierClient.Id;
                existing.Code = normalizedCode;
                existing.Name = supplierName;
                ApplyAgnumSupplierFields(existing, supplierClient);
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                imported++;
                continue;
            }

            var supplier = new Supplier
            {
                AgnumClientId = supplierClient.Id,
                Code = normalizedCode,
                Name = supplierName,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };
            ApplyAgnumSupplierFields(supplier, supplierClient);

            _dbContext.Suppliers.Add(supplier);
            existingSuppliersByCode[normalizedCode] = supplier;
            if (supplierClient.Id > 0)
            {
                existingSuppliersByAgnumId[supplierClient.Id] = supplier;
            }

            imported++;
        }

        _logger.LogInformation("Prepared {SupplierCount} Agnum suppliers for import.", imported);
    }

    private async Task EnsureCustomersAsync(IReadOnlyList<AgnumClientDto> customers, CancellationToken ct)
    {
        if (customers.Count == 0)
        {
            return;
        }

        var normalizedCodes = customers
            .Select(x => NormalizeCustomerCode(x.Code))
            .Where(x => x is not null)
            .Select(x => x!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var agnumClientIds = customers
            .Where(x => x.Id > 0)
            .Select(x => x.Id)
            .Distinct()
            .ToList();

        var normalizedCodeUpperSet = normalizedCodes
            .Select(x => x.ToUpperInvariant())
            .ToHashSet(StringComparer.Ordinal);

        var existingCustomers = await _dbContext.Customers
            .IgnoreQueryFilters()
            .Where(x => normalizedCodeUpperSet.Contains(x.CustomerCode.ToUpper()) || x.AgnumClientId != null && agnumClientIds.Contains(x.AgnumClientId.Value))
            .ToListAsync(ct);
        var existingCustomersByCode = existingCustomers
            .ToDictionary(x => x.CustomerCode, StringComparer.OrdinalIgnoreCase);
        var existingCustomersByAgnumId = existingCustomers
            .Where(x => x.AgnumClientId is not null)
            .ToDictionary(x => x.AgnumClientId!.Value);

        var imported = 0;
        foreach (var client in customers)
        {
            var normalizedCode = NormalizeCustomerCode(client.Code);
            if (normalizedCode is null)
            {
                continue;
            }

            var customerName = string.IsNullOrWhiteSpace(client.Name)
                ? normalizedCode
                : client.Name.Trim();
            var customerEmail = client.Email?.Trim() ?? string.Empty;
            var billingAddress = BuildCustomerAddress(client);

            var tracked = _dbContext.Customers.Local.FirstOrDefault(x =>
                x.AgnumClientId == client.Id
                || string.Equals(x.CustomerCode, normalizedCode, StringComparison.OrdinalIgnoreCase));
            if (tracked is not null)
            {
                tracked.AgnumClientId = client.Id;
                tracked.Name = customerName;
                tracked.Email = customerEmail;
                tracked.BillingAddress = billingAddress;
                tracked.Status = CustomerStatus.Active;
                tracked.IsDeleted = false;
                tracked.UpdatedAt = DateTimeOffset.UtcNow;
                imported++;
                continue;
            }

            if ((client.Id > 0 && existingCustomersByAgnumId.TryGetValue(client.Id, out var existing))
                || existingCustomersByCode.TryGetValue(normalizedCode, out existing))
            {
                existing.AgnumClientId = client.Id;
                existing.CustomerCode = normalizedCode;
                existing.Name = customerName;
                existing.Email = customerEmail;
                existing.BillingAddress = billingAddress;
                existing.Status = CustomerStatus.Active;
                existing.IsDeleted = false;
                existing.UpdatedAt = DateTimeOffset.UtcNow;
                imported++;
                continue;
            }

            var customer = new Customer
            {
                AgnumClientId = client.Id,
                CustomerCode = normalizedCode,
                Name = customerName,
                Email = customerEmail,
                BillingAddress = billingAddress,
                Status = CustomerStatus.Active,
                PaymentTerms = PaymentTerms.Net30,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            _dbContext.Customers.Add(customer);
            existingCustomersByCode[normalizedCode] = customer;
            if (client.Id > 0)
            {
                existingCustomersByAgnumId[client.Id] = customer;
            }

            imported++;
        }

        _logger.LogInformation("Prepared {CustomerCount} Agnum customers for import.", imported);
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

    private static string? NormalizeCustomerCode(string? value)
        => NormalizeSupplierCode(value);

    private static bool IsSupplier(AgnumClientDto client)
    {
        return client.ClientRoles?.Any(x => string.Equals(x, "SUPPLIER", StringComparison.OrdinalIgnoreCase)) == true
            || client.PozymNumbers?.Contains(1) == true;
    }

    private static bool IsBuyer(AgnumClientDto client)
    {
        return client.ClientRoles?.Any(x => string.Equals(x, "BUYER", StringComparison.OrdinalIgnoreCase)) == true
            || client.PozymNumbers?.Contains(2) == true;
    }

    private static Address BuildCustomerAddress(AgnumClientDto client)
    {
        var address = string.IsNullOrWhiteSpace(client.RegisteredAddress)
            ? client.OfficeAddress
            : client.RegisteredAddress;

        return new Address
        {
            Street = address?.Trim() ?? string.Empty
        };
    }

    /// <summary>
    /// Maps Agnum-owned client fields onto the structured supplier columns. Agnum is the source of
    /// truth for values it provides, but it never overwrites a non-empty manual value with a blank
    /// (Agnum does not currently expose phone/contact/website, so those manual fields stay intact).
    /// </summary>
    private static void ApplyAgnumSupplierFields(Supplier supplier, AgnumClientDto client)
    {
        supplier.CompanyCode = MergeAgnumValue(supplier.CompanyCode, client.CompanyCode);
        supplier.VatCode = MergeAgnumValue(supplier.VatCode, client.VatCode);
        supplier.Email = MergeAgnumValue(supplier.Email, client.Email);
        supplier.RegisteredAddress = MergeAgnumValue(supplier.RegisteredAddress, client.RegisteredAddress);
        // Agnum's officeAddress is the pickup/dispatch location for the supplier relationship.
        supplier.PickupAddress = MergeAgnumValue(supplier.PickupAddress, client.OfficeAddress);
        supplier.LastAgnumSyncedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Returns the trimmed Agnum value when present; otherwise keeps the existing (manual) value.
    /// A blank incoming value never clears a stored manual correction.
    /// </summary>
    private static string? MergeAgnumValue(string? existing, string? incoming)
        => string.IsNullOrWhiteSpace(incoming) ? existing : incoming.Trim();

    private static decimal? NormalizePositiveDecimal(decimal? value)
    {
        return value > 0 ? value : null;
    }

    private static string NormalizeSkuCode(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static DateTime? NormalizeUtcDateTime(DateTime? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
        };
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
