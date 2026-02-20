using ClosedXML.Excel;
using EFCore.BulkExtensions;
using LKvitai.MES.Modules.Warehouse.Application.Models;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Infrastructure.Imports;

public interface IMasterDataImportService
{
    Task<ImportExecutionResult> ImportAsync(
        string entityType,
        Stream stream,
        bool dryRun,
        CancellationToken cancellationToken = default);

    byte[] GenerateErrorReport(IReadOnlyList<ImportErrorReport> errors);
}

public sealed record ImportExecutionResult(
    int Inserted,
    int Updated,
    int Skipped,
    bool DryRun,
    bool UsedBulk,
    IReadOnlyList<ImportErrorReport> Errors,
    TimeSpan Duration);

public sealed class MasterDataImportService : IMasterDataImportService
{
    private readonly WarehouseDbContext _dbContext;
    private readonly ISkuGenerationService _skuGenerationService;

    public MasterDataImportService(
        WarehouseDbContext dbContext,
        ISkuGenerationService skuGenerationService)
    {
        _dbContext = dbContext;
        _skuGenerationService = skuGenerationService;
    }

    public async Task<ImportExecutionResult> ImportAsync(
        string entityType,
        Stream stream,
        bool dryRun,
        CancellationToken cancellationToken = default)
    {
        var startedAt = DateTime.UtcNow;
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        var errors = new List<ImportErrorReport>();

        var normalized = entityType.Trim().ToLowerInvariant();
        var headers = normalized switch
        {
            "items" => ExcelTemplateService.ItemHeaders,
            "suppliers" => ExcelTemplateService.SupplierHeaders,
            "mappings" => ExcelTemplateService.MappingHeaders,
            "barcodes" => ExcelTemplateService.BarcodeHeaders,
            "locations" => ExcelTemplateService.LocationHeaders,
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), $"Unsupported import entity '{entityType}'.")
        };

        ValidateHeaders(sheet, headers, errors);
        if (errors.Count > 0)
        {
            return BuildResult(0, 0, 0, dryRun, false, errors, startedAt);
        }

        var rows = ReadRows(sheet, headers);
        if (rows.Count == 0)
        {
            return BuildResult(0, 0, 0, dryRun, false, errors, startedAt);
        }

        return normalized switch
        {
            "items" => await ImportItemsAsync(rows, dryRun, errors, startedAt, cancellationToken),
            "suppliers" => await ImportSuppliersAsync(rows, dryRun, errors, startedAt, cancellationToken),
            "mappings" => await ImportMappingsAsync(rows, dryRun, errors, startedAt, cancellationToken),
            "barcodes" => await ImportBarcodesAsync(rows, dryRun, errors, startedAt, cancellationToken),
            "locations" => await ImportLocationsAsync(rows, dryRun, errors, startedAt, cancellationToken),
            _ => BuildResult(0, 0, rows.Count, dryRun, false, errors, startedAt)
        };
    }

    public byte[] GenerateErrorReport(IReadOnlyList<ImportErrorReport> errors)
    {
        var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Errors");

        var headers = new[] { "Row", "Column", "InvalidValue", "Message" };
        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        for (var i = 0; i < errors.Count; i++)
        {
            var row = i + 2;
            sheet.Cell(row, 1).Value = errors[i].Row;
            sheet.Cell(row, 2).Value = errors[i].Column;
            sheet.Cell(row, 3).Value = errors[i].Value ?? string.Empty;
            sheet.Cell(row, 4).Value = errors[i].Message;
        }

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task<ImportExecutionResult> ImportItemsAsync(
        IReadOnlyList<RowData> rows,
        bool dryRun,
        List<ImportErrorReport> errors,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var categoryMap = await _dbContext.ItemCategories
            .AsNoTracking()
            .ToDictionaryAsync(c => c.Code, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var uoms = await _dbContext.UnitOfMeasures
            .AsNoTracking()
            .Select(u => u.Code)
            .ToListAsync(cancellationToken);
        var uomSet = new HashSet<string>(uoms, StringComparer.OrdinalIgnoreCase);
        var existingItems = await _dbContext.Items
            .AsNoTracking()
            .ToDictionaryAsync(i => i.InternalSKU, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var barcodeList = await _dbContext.ItemBarcodes
            .AsNoTracking()
            .Select(b => b.Barcode)
            .ToListAsync(cancellationToken);
        var existingBarcodes = new HashSet<string>(barcodeList, StringComparer.OrdinalIgnoreCase);

        var seenSku = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenBarcode = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toInsert = new List<Item>();
        var toUpdate = new List<Item>();

        foreach (var row in rows)
        {
            var name = row.Get("Name");
            var categoryCode = row.Get("CategoryCode");
            var baseUom = row.Get("BaseUoM");
            var sku = row.Get("InternalSKU");
            var primaryBarcode = row.Get("PrimaryBarcode");

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "Name", name, "Name is required."));
            }

            if (string.IsNullOrWhiteSpace(categoryCode) || !categoryMap.ContainsKey(categoryCode))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "CategoryCode", categoryCode, "CategoryCode does not exist."));
            }

            if (string.IsNullOrWhiteSpace(baseUom) || !uomSet.Contains(baseUom))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "BaseUoM", baseUom, "BaseUoM does not exist."));
            }

            if (!string.IsNullOrWhiteSpace(primaryBarcode))
            {
                if (!seenBarcode.Add(primaryBarcode) || existingBarcodes.Contains(primaryBarcode))
                {
                    errors.Add(new ImportErrorReport(row.RowNumber, "PrimaryBarcode", primaryBarcode, "Barcode must be unique."));
                }
            }

            if (!string.IsNullOrWhiteSpace(sku))
            {
                if (!seenSku.Add(sku))
                {
                    errors.Add(new ImportErrorReport(row.RowNumber, "InternalSKU", sku, "Duplicate SKU in file."));
                }
            }
        }

        if (errors.Count > 0)
        {
            return BuildResult(0, 0, rows.Count, dryRun, false, errors, startedAt);
        }

        // Dry-run preview sequence map (no database writes)
        var nextByPrefix = await _dbContext.SKUSequences
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Prefix, x => x.NextValue, StringComparer.OrdinalIgnoreCase, cancellationToken);

        foreach (var row in rows)
        {
            var sku = row.Get("InternalSKU");
            var categoryCode = row.Get("CategoryCode")!;
            var categoryId = categoryMap[categoryCode];

            if (string.IsNullOrWhiteSpace(sku))
            {
                if (dryRun)
                {
                    var prefix = GetPrefix(categoryCode);
                    var next = nextByPrefix.TryGetValue(prefix, out var value) ? value : 1;
                    sku = $"{prefix}-{next:D4}";
                    nextByPrefix[prefix] = next + 1;
                }
                else
                {
                    sku = await _skuGenerationService.GenerateNextSkuAsync(categoryId, cancellationToken);
                }
            }

            existingItems.TryGetValue(sku, out var existing);
            if (existing is null)
            {
                toInsert.Add(new Item
                {
                    InternalSKU = sku,
                    Name = row.Get("Name")!,
                    Description = row.Get("Description"),
                    CategoryId = categoryId,
                    BaseUoM = row.Get("BaseUoM")!,
                    Weight = ParseDecimal(row.Get("Weight")),
                    Volume = ParseDecimal(row.Get("Volume")),
                    RequiresLotTracking = ParseBool(row.Get("RequiresLotTracking")),
                    RequiresQC = ParseBool(row.Get("RequiresQC")),
                    Status = string.IsNullOrWhiteSpace(row.Get("Status")) ? "Active" : row.Get("Status")!,
                    PrimaryBarcode = row.Get("PrimaryBarcode"),
                    ProductConfigId = row.Get("ProductConfigId")
                });
            }
            else
            {
                existing.Name = row.Get("Name")!;
                existing.Description = row.Get("Description");
                existing.CategoryId = categoryId;
                existing.BaseUoM = row.Get("BaseUoM")!;
                existing.Weight = ParseDecimal(row.Get("Weight"));
                existing.Volume = ParseDecimal(row.Get("Volume"));
                existing.RequiresLotTracking = ParseBool(row.Get("RequiresLotTracking"));
                existing.RequiresQC = ParseBool(row.Get("RequiresQC"));
                existing.Status = string.IsNullOrWhiteSpace(row.Get("Status")) ? existing.Status : row.Get("Status")!;
                existing.PrimaryBarcode = row.Get("PrimaryBarcode");
                existing.ProductConfigId = row.Get("ProductConfigId");
                toUpdate.Add(existing);
            }
        }

        var usedBulk = !dryRun && rows.Count > 1000;
        if (!dryRun)
        {
            if (usedBulk)
            {
                if (toInsert.Count > 0) await _dbContext.BulkInsertAsync(toInsert, cancellationToken: cancellationToken);
                if (toUpdate.Count > 0) await _dbContext.BulkUpdateAsync(toUpdate, cancellationToken: cancellationToken);
            }
            else
            {
                if (toInsert.Count > 0) await _dbContext.Items.AddRangeAsync(toInsert, cancellationToken);
                if (toUpdate.Count > 0) _dbContext.Items.UpdateRange(toUpdate);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return BuildResult(toInsert.Count, toUpdate.Count, 0, dryRun, usedBulk, errors, startedAt);
    }

    private async Task<ImportExecutionResult> ImportSuppliersAsync(
        IReadOnlyList<RowData> rows,
        bool dryRun,
        List<ImportErrorReport> errors,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Suppliers
            .AsNoTracking()
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var toInsert = new List<Supplier>();
        var toUpdate = new List<Supplier>();

        foreach (var row in rows)
        {
            var code = row.Get("Code");
            var name = row.Get("Name");
            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "Code", code, "Code is required."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "Name", name, "Name is required."));
                continue;
            }

            if (!seen.Add(code))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "Code", code, "Duplicate supplier code in file."));
                continue;
            }

            if (existing.TryGetValue(code, out var current))
            {
                current.Name = name;
                current.ContactInfo = row.Get("ContactInfo");
                toUpdate.Add(current);
            }
            else
            {
                toInsert.Add(new Supplier
                {
                    Code = code,
                    Name = name,
                    ContactInfo = row.Get("ContactInfo")
                });
            }
        }

        if (errors.Count > 0)
        {
            return BuildResult(0, 0, rows.Count, dryRun, false, errors, startedAt);
        }

        var usedBulk = !dryRun && rows.Count > 1000;
        if (!dryRun)
        {
            if (usedBulk)
            {
                if (toInsert.Count > 0) await _dbContext.BulkInsertAsync(toInsert, cancellationToken: cancellationToken);
                if (toUpdate.Count > 0) await _dbContext.BulkUpdateAsync(toUpdate, cancellationToken: cancellationToken);
            }
            else
            {
                if (toInsert.Count > 0) await _dbContext.Suppliers.AddRangeAsync(toInsert, cancellationToken);
                if (toUpdate.Count > 0) _dbContext.Suppliers.UpdateRange(toUpdate);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        return BuildResult(toInsert.Count, toUpdate.Count, 0, dryRun, usedBulk, errors, startedAt);
    }

    private async Task<ImportExecutionResult> ImportMappingsAsync(
        IReadOnlyList<RowData> rows,
        bool dryRun,
        List<ImportErrorReport> errors,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var supplierMap = await _dbContext.Suppliers.AsNoTracking()
            .ToDictionaryAsync(x => x.Code, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var itemMap = await _dbContext.Items.AsNoTracking()
            .ToDictionaryAsync(x => x.InternalSKU, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var existing = await _dbContext.SupplierItemMappings.AsNoTracking()
            .ToListAsync(cancellationToken);

        var toInsert = new List<SupplierItemMapping>();
        var toUpdate = new List<SupplierItemMapping>();

        foreach (var row in rows)
        {
            var supplierCode = row.Get("SupplierCode");
            var supplierSku = row.Get("SupplierSKU");
            var itemSku = row.Get("ItemSKU");

            if (string.IsNullOrWhiteSpace(supplierCode) || !supplierMap.TryGetValue(supplierCode, out var supplierId))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "SupplierCode", supplierCode, "SupplierCode does not exist."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(itemSku) || !itemMap.TryGetValue(itemSku, out var itemId))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "ItemSKU", itemSku, "ItemSKU does not exist."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(supplierSku))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "SupplierSKU", supplierSku, "SupplierSKU is required."));
                continue;
            }

            var current = existing.FirstOrDefault(x =>
                x.SupplierId == supplierId &&
                string.Equals(x.SupplierSKU, supplierSku, StringComparison.OrdinalIgnoreCase));

            if (current is null)
            {
                toInsert.Add(new SupplierItemMapping
                {
                    SupplierId = supplierId,
                    SupplierSKU = supplierSku,
                    ItemId = itemId,
                    LeadTimeDays = ParseInt(row.Get("LeadTimeDays")),
                    MinOrderQty = ParseDecimal(row.Get("MinOrderQty")),
                    PricePerUnit = ParseDecimal(row.Get("PricePerUnit"))
                });
            }
            else
            {
                current.ItemId = itemId;
                current.LeadTimeDays = ParseInt(row.Get("LeadTimeDays"));
                current.MinOrderQty = ParseDecimal(row.Get("MinOrderQty"));
                current.PricePerUnit = ParseDecimal(row.Get("PricePerUnit"));
                toUpdate.Add(current);
            }
        }

        if (errors.Count > 0)
        {
            return BuildResult(0, 0, rows.Count, dryRun, false, errors, startedAt);
        }

        if (!dryRun)
        {
            if (toInsert.Count > 0) await _dbContext.SupplierItemMappings.AddRangeAsync(toInsert, cancellationToken);
            if (toUpdate.Count > 0) _dbContext.SupplierItemMappings.UpdateRange(toUpdate);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return BuildResult(toInsert.Count, toUpdate.Count, 0, dryRun, false, errors, startedAt);
    }

    private async Task<ImportExecutionResult> ImportBarcodesAsync(
        IReadOnlyList<RowData> rows,
        bool dryRun,
        List<ImportErrorReport> errors,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var itemMap = await _dbContext.Items.AsNoTracking()
            .ToDictionaryAsync(x => x.InternalSKU, x => x.Id, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var existing = await _dbContext.ItemBarcodes.AsNoTracking()
            .ToDictionaryAsync(x => x.Barcode, StringComparer.OrdinalIgnoreCase, cancellationToken);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var toInsert = new List<ItemBarcode>();
        var toUpdate = new List<ItemBarcode>();

        foreach (var row in rows)
        {
            var sku = row.Get("ItemSKU");
            var barcode = row.Get("Barcode");
            if (string.IsNullOrWhiteSpace(sku) || !itemMap.TryGetValue(sku, out var itemId))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "ItemSKU", sku, "ItemSKU does not exist."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(barcode))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "Barcode", barcode, "Barcode is required."));
                continue;
            }

            if (!seen.Add(barcode))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "Barcode", barcode, "Duplicate barcode in file."));
                continue;
            }

            if (existing.TryGetValue(barcode, out var current))
            {
                current.ItemId = itemId;
                current.BarcodeType = string.IsNullOrWhiteSpace(row.Get("BarcodeType")) ? "Code128" : row.Get("BarcodeType")!;
                current.IsPrimary = ParseBool(row.Get("IsPrimary"));
                toUpdate.Add(current);
            }
            else
            {
                toInsert.Add(new ItemBarcode
                {
                    ItemId = itemId,
                    Barcode = barcode,
                    BarcodeType = string.IsNullOrWhiteSpace(row.Get("BarcodeType")) ? "Code128" : row.Get("BarcodeType")!,
                    IsPrimary = ParseBool(row.Get("IsPrimary"))
                });
            }
        }

        if (errors.Count > 0)
        {
            return BuildResult(0, 0, rows.Count, dryRun, false, errors, startedAt);
        }

        if (!dryRun)
        {
            if (toInsert.Count > 0) await _dbContext.ItemBarcodes.AddRangeAsync(toInsert, cancellationToken);
            if (toUpdate.Count > 0) _dbContext.ItemBarcodes.UpdateRange(toUpdate);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return BuildResult(toInsert.Count, toUpdate.Count, 0, dryRun, false, errors, startedAt);
    }

    private async Task<ImportExecutionResult> ImportLocationsAsync(
        IReadOnlyList<RowData> rows,
        bool dryRun,
        List<ImportErrorReport> errors,
        DateTime startedAt,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.Locations.AsNoTracking()
            .ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var toInsert = new List<Location>();
        var toUpdate = new List<Location>();

        foreach (var row in rows)
        {
            var code = row.Get("Code");
            var barcode = row.Get("Barcode");
            var type = row.Get("Type");

            if (string.IsNullOrWhiteSpace(code))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "Code", code, "Code is required."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(barcode))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "Barcode", barcode, "Barcode is required."));
                continue;
            }

            if (string.IsNullOrWhiteSpace(type))
            {
                errors.Add(new ImportErrorReport(row.RowNumber, "Type", type, "Type is required."));
                continue;
            }

            var parentCode = row.Get("ParentCode");
            int? parentId = null;
            if (!string.IsNullOrWhiteSpace(parentCode))
            {
                if (existing.TryGetValue(parentCode, out var parent))
                {
                    parentId = parent.Id;
                }
                else
                {
                    errors.Add(new ImportErrorReport(row.RowNumber, "ParentCode", parentCode, "Parent location does not exist."));
                    continue;
                }
            }

            if (existing.TryGetValue(code, out var current))
            {
                current.Barcode = barcode;
                current.Type = type;
                current.ParentLocationId = parentId;
                current.IsVirtual = ParseBool(row.Get("IsVirtual"));
                current.MaxWeight = ParseDecimal(row.Get("MaxWeight"));
                current.MaxVolume = ParseDecimal(row.Get("MaxVolume"));
                current.Status = string.IsNullOrWhiteSpace(row.Get("Status")) ? "Active" : row.Get("Status")!;
                current.ZoneType = row.Get("ZoneType");
                toUpdate.Add(current);
            }
            else
            {
                toInsert.Add(new Location
                {
                    Code = code,
                    Barcode = barcode,
                    Type = type,
                    ParentLocationId = parentId,
                    IsVirtual = ParseBool(row.Get("IsVirtual")),
                    MaxWeight = ParseDecimal(row.Get("MaxWeight")),
                    MaxVolume = ParseDecimal(row.Get("MaxVolume")),
                    Status = string.IsNullOrWhiteSpace(row.Get("Status")) ? "Active" : row.Get("Status")!,
                    ZoneType = row.Get("ZoneType")
                });
            }
        }

        if (errors.Count > 0)
        {
            return BuildResult(0, 0, rows.Count, dryRun, false, errors, startedAt);
        }

        if (!dryRun)
        {
            if (toInsert.Count > 0) await _dbContext.Locations.AddRangeAsync(toInsert, cancellationToken);
            if (toUpdate.Count > 0) _dbContext.Locations.UpdateRange(toUpdate);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return BuildResult(toInsert.Count, toUpdate.Count, 0, dryRun, false, errors, startedAt);
    }

    private static List<RowData> ReadRows(IXLWorksheet sheet, IReadOnlyList<string> headers)
    {
        var rows = new List<RowData>();
        var lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var row = 2; row <= lastRow; row++)
        {
            var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
            var hasValue = false;
            for (var col = 0; col < headers.Count; col++)
            {
                var value = sheet.Cell(row, col + 1).GetString()?.Trim();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    hasValue = true;
                }

                values[headers[col]] = string.IsNullOrWhiteSpace(value) ? null : value;
            }

            if (hasValue)
            {
                rows.Add(new RowData(row, values));
            }
        }

        return rows;
    }

    private static void ValidateHeaders(
        IXLWorksheet sheet,
        IReadOnlyList<string> expected,
        ICollection<ImportErrorReport> errors)
    {
        for (var i = 0; i < expected.Count; i++)
        {
            var current = sheet.Cell(1, i + 1).GetString()?.Trim();
            if (!string.Equals(current, expected[i], StringComparison.OrdinalIgnoreCase))
            {
                errors.Add(new ImportErrorReport(1, expected[i], current, $"Expected column '{expected[i]}'."));
            }
        }

        var lastHeaderColumn = sheet.Row(1).LastCellUsed()?.Address.ColumnNumber ?? expected.Count;
        for (var col = expected.Count + 1; col <= lastHeaderColumn; col++)
        {
            var unexpected = sheet.Cell(1, col).GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(unexpected))
            {
                errors.Add(new ImportErrorReport(1, unexpected, unexpected, $"Unexpected column '{unexpected}'."));
            }
        }
    }

    private static decimal? ParseDecimal(string? value)
        => decimal.TryParse(value, out var parsed) ? parsed : null;

    private static int? ParseInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static bool ParseBool(string? value)
        => bool.TryParse(value, out var parsed) && parsed;

    private static string GetPrefix(string categoryCode)
    {
        if (string.Equals(categoryCode, "RAW", StringComparison.OrdinalIgnoreCase))
        {
            return "RM";
        }

        if (string.Equals(categoryCode, "FINISHED", StringComparison.OrdinalIgnoreCase))
        {
            return "FG";
        }

        return "ITEM";
    }

    private static ImportExecutionResult BuildResult(
        int inserted,
        int updated,
        int skipped,
        bool dryRun,
        bool usedBulk,
        IReadOnlyList<ImportErrorReport> errors,
        DateTime startedAt)
        => new(
            inserted,
            updated,
            skipped,
            dryRun,
            usedBulk,
            errors,
            DateTime.UtcNow - startedAt);

    private sealed record RowData(int RowNumber, IReadOnlyDictionary<string, string?> Values)
    {
        public string? Get(string key)
            => Values.TryGetValue(key, out var value) ? value : null;
    }
}
