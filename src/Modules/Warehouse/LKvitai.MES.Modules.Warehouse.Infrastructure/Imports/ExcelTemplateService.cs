using ClosedXML.Excel;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Imports;

public interface IExcelTemplateService
{
    Task<byte[]> GenerateTemplateAsync(string entityType, CancellationToken cancellationToken = default);
}

public sealed class ExcelTemplateService : IExcelTemplateService
{
    private readonly WarehouseDbContext _dbContext;

    public ExcelTemplateService(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<byte[]> GenerateTemplateAsync(
        string entityType,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeEntityType(entityType);
        var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Import");

        var headers = normalized switch
        {
            "items" => ItemHeaders,
            "suppliers" => SupplierHeaders,
            "mappings" => MappingHeaders,
            "barcodes" => BarcodeHeaders,
            "locations" => LocationHeaders,
            _ => throw new ArgumentOutOfRangeException(nameof(entityType), $"Unsupported import entity '{entityType}'.")
        };

        for (var i = 0; i < headers.Length; i++)
        {
            sheet.Cell(1, i + 1).Value = headers[i];
            sheet.Cell(1, i + 1).Style.Font.Bold = true;
        }

        var sample = BuildSampleRow(normalized);
        for (var i = 0; i < sample.Length; i++)
        {
            sheet.Cell(2, i + 1).Value = sample[i];
        }

        if (normalized == "items")
        {
            await ApplyItemDropdownValidationAsync(sheet, cancellationToken);
        }

        sheet.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    private async Task ApplyItemDropdownValidationAsync(IXLWorksheet sheet, CancellationToken cancellationToken)
    {
        var categories = await _dbContext.ItemCategories
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => c.Code)
            .ToListAsync(cancellationToken);
        var uoms = await _dbContext.UnitOfMeasures
            .AsNoTracking()
            .OrderBy(u => u.Code)
            .Select(u => u.Code)
            .ToListAsync(cancellationToken);

        if (categories.Count == 0 || uoms.Count == 0)
        {
            return;
        }

        var lookup = sheet.Workbook.Worksheets.Add("Lookup");
        lookup.Visibility = XLWorksheetVisibility.VeryHidden;

        for (var i = 0; i < categories.Count; i++)
        {
            lookup.Cell(i + 1, 1).Value = categories[i];
        }

        for (var i = 0; i < uoms.Count; i++)
        {
            lookup.Cell(i + 1, 2).Value = uoms[i];
        }

        var categoryRange = $"Lookup!$A$1:$A${categories.Count}";
        var uomRange = $"Lookup!$B$1:$B${uoms.Count}";

        var categoryValidation = sheet.Range("D2:D5000").CreateDataValidation();
        categoryValidation.IgnoreBlanks = true;
        categoryValidation.InCellDropdown = true;
        categoryValidation.AllowedValues = XLAllowedValues.List;
        categoryValidation.List(categoryRange, true);

        var uomValidation = sheet.Range("E2:E5000").CreateDataValidation();
        uomValidation.IgnoreBlanks = true;
        uomValidation.InCellDropdown = true;
        uomValidation.AllowedValues = XLAllowedValues.List;
        uomValidation.List(uomRange, true);
    }

    private static string[] BuildSampleRow(string normalizedEntityType) => normalizedEntityType switch
    {
        "items" => ["", "Steel Bolt M8", "High-strength bolt", "RAW", "PCS", "0.015", "0.0001", "false", "false", "Active", "8594156780187", ""],
        "suppliers" => ["SUP-001", "ABC Fasteners Ltd", "{\"email\":\"orders@abc.com\"}"],
        "mappings" => ["SUP-001", "ABC-M8-BOLT", "RM-0001", "7", "100", "0.45"],
        "barcodes" => ["RM-0001", "8594156780187", "EAN13", "true"],
        "locations" => ["WH01-A-01", "LOC-WH01-A-01", "Bin", "", "false", "500", "10", "Active", "General"],
        _ => []
    };

    private static string NormalizeEntityType(string entityType)
        => entityType.Trim().ToLowerInvariant();

    public static readonly string[] ItemHeaders =
    [
        "InternalSKU", "Name", "Description", "CategoryCode", "BaseUoM", "Weight", "Volume",
        "RequiresLotTracking", "RequiresQC", "Status", "PrimaryBarcode", "ProductConfigId"
    ];

    public static readonly string[] SupplierHeaders =
    [
        "Code", "Name", "ContactInfo"
    ];

    public static readonly string[] MappingHeaders =
    [
        "SupplierCode", "SupplierSKU", "ItemSKU", "LeadTimeDays", "MinOrderQty", "PricePerUnit"
    ];

    public static readonly string[] BarcodeHeaders =
    [
        "ItemSKU", "Barcode", "BarcodeType", "IsPrimary"
    ];

    public static readonly string[] LocationHeaders =
    [
        "Code", "Barcode", "Type", "ParentCode", "IsVirtual", "MaxWeight", "MaxVolume", "Status", "ZoneType"
    ];
}
