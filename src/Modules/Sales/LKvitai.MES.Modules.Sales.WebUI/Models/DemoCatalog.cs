using System.Text.Json;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;

namespace LKvitai.MES.Modules.Sales.WebUI.Models;

/// <summary>
/// Milestone 1 reference snapshot. It is bundled into the WebUI assembly and
/// never queried from SQL at runtime. Replace with API-backed lookups in M2.
/// </summary>
public sealed class DemoCatalog
{
    public string SnapshotDate { get; init; } = "";
    public List<DemoSeries> Series { get; init; } = [];
    public List<DemoCategory> Categories { get; init; } = [];
    public List<DemoAccessory> Accessories { get; init; } = [];
    public List<DemoColour> Colours { get; init; } = [];

    public List<LiveNamedReferenceDto> Customers { get; init; } = [];
    public List<LiveNamedReferenceDto> Employees { get; init; } = [];
    public List<LiveNamedReferenceDto> Stores { get; init; } = [];
    public static DemoCatalog FromLive(LiveOrderEntryCatalogDto live) => new()
    {
        SnapshotDate = live.LoadedAtUtc.ToString("yyyy-MM-dd HH:mm"),
        Series = live.Series.Select(x => new DemoSeries { Id=x.Id, Code=x.Code,
            Customer=x.CustomerName ?? "", CustomerId=x.CustomerId, Store=x.StoreName ?? "", StoreId=x.StoreId,
            Manager=x.ManagerName ?? "", ManagerId=x.ManagerId, Consultant=x.ConsultantName ?? "", ConsultantId=x.ConsultantId,
            BranchId=x.BranchId }).ToList(),
        Categories = live.Categories.Select(x => new DemoCategory { Id=x.Id, Name=x.Name, Active=x.IsActive,
            DiscountAllowed=x.DiscountAllowed, ParameterLabels=x.ParameterLabels.ToList(),
            Types=live.Types.Where(t=>t.CategoryId==x.Id).Select(t=>new DemoType { Id=t.Id, Name=t.Name,
                IsManufactured=t.IsManufactured, CanEnterManualPrice=t.CanEnterManualPrice, UnsupportedReason=t.UnsupportedReason,
                ParameterFormulas=t.ParameterFormulas.ToList() }).ToList() }).ToList(),
        Colours=live.Colours.Select(x=>new DemoColour { Id=x.Id, Code=x.Code, Name=x.Name, CategoryIds=x.CategoryIds.ToList(), FreeQuantity=x.FreeQuantity, WarehouseQuantity=x.WarehouseQuantity, ProductionQuantity=x.ProductionQuantity, TotalQuantity=x.TotalQuantity, ReservedQuantity=x.ReservedQuantity, Unit=x.Unit }).ToList(),
        Accessories=live.Accessories.Select(x=>new DemoAccessory { Id=x.Id, Name=x.Name, Unit=x.Unit,
            Price=x.Price ?? 0, HasReferencePrice=x.Price.HasValue, CategoryIds=x.CategoryIds.ToList() }).ToList(),
        Customers=live.Customers.ToList(), Employees=live.Employees.ToList(), Stores=live.Stores.ToList()
    };

    public static DemoCatalog Load()
    {
        var assembly = typeof(DemoCatalog).Assembly;
        const string resource = "LKvitai.MES.Modules.Sales.WebUI.Data.demo-catalog.json";
        using var stream = assembly.GetManifestResourceStream(resource)
            ?? throw new InvalidOperationException($"Missing embedded Sales demo catalog: {resource}");
        return JsonSerializer.Deserialize<DemoCatalog>(stream, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? throw new InvalidOperationException("Sales demo catalog is empty.");
    }
}

public sealed class DemoSeries
{
    public int? BranchId { get; init; }
    public int? CustomerId { get; init; }
    public int? StoreId { get; init; }
    public int? ManagerId { get; init; }
    public int? ConsultantId { get; init; }
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Customer { get; init; } = "";
    public string Manager { get; init; } = "";
    public string Store { get; init; } = "";
    public string Consultant { get; init; } = "";
}

public sealed class DemoCategory
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public bool Active { get; init; }
    public bool DiscountAllowed { get; init; }
    public List<string?> ParameterLabels { get; init; } = [];
    public List<DemoType> Types { get; init; } = [];
}

public sealed class DemoType
{
    public string? UnsupportedReason { get; init; }
    public bool IsManufactured { get; init; } = true;
    public bool CanEnterManualPrice { get; init; } = true;
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public List<string?> ParameterFormulas { get; init; } = [];
}

public sealed class DemoAccessory
{
    public int Id { get; init; }
    private string? _name;
    public string Name
    {
        get => string.IsNullOrWhiteSpace(_name) ? LKvitai.MES.Modules.Sales.WebUI.Resources.SalesResourceText.Localizer["Catalog.UnnamedAccessory", Id].Value : _name;
        init => _name = value;
    }
    public string Unit { get; init; } = "";
    public decimal Price { get; init; }
    public bool HasReferencePrice { get; init; } = true;
    public List<int> CategoryIds { get; init; } = [];
}

public sealed class DemoColour
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public List<int> CategoryIds { get; init; } = [];
    public decimal? FreeQuantity { get; init; }
    public decimal? WarehouseQuantity { get; init; }
    public decimal? ProductionQuantity { get; init; }
    public decimal? TotalQuantity { get; init; }
    public decimal? ReservedQuantity { get; init; }
    public string? Unit { get; init; }
}
