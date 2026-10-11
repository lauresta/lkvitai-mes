using System.Text.Json;

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
    public List<string?> ParameterLabels { get; init; } = [];
    public List<DemoType> Types { get; init; } = [];
}

public sealed class DemoType
{
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
        get => string.IsNullOrWhiteSpace(_name) ? $"Priedas #{Id}" : _name;
        init => _name = value;
    }
    public string Unit { get; init; } = "";
    public decimal Price { get; init; }
    public List<int> CategoryIds { get; init; } = [];
}

public sealed class DemoColour
{
    public int Id { get; init; }
    public string Code { get; init; } = "";
    public string Name { get; init; } = "";
    public List<int> CategoryIds { get; init; } = [];
}
