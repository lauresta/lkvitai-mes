using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public enum LabelTemplateType
{
    Location,
    HandlingUnit,
    Item
}

public sealed class LabelTemplate
{
    public required LabelTemplateType Type { get; init; }
    public required string ZplTemplate { get; init; }
}

public sealed class LabelData
{
    public Dictionary<string, string> Placeholders { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class LabelTemplateEngine
{
    private static readonly Regex PlaceholderPattern =
        new("{{\\s*(?<key>[A-Za-z0-9_]+)\\s*}}", RegexOptions.Compiled);

    private readonly IReadOnlyDictionary<LabelTemplateType, string> _templates;

    public LabelTemplateEngine(IConfiguration configuration)
    {
        _templates = new Dictionary<LabelTemplateType, string>
        {
            [LabelTemplateType.Location] = configuration["Labels:Templates:Location"] ?? DefaultLocationTemplate,
            [LabelTemplateType.HandlingUnit] = configuration["Labels:Templates:HandlingUnit"] ?? DefaultHandlingUnitTemplate,
            [LabelTemplateType.Item] = configuration["Labels:Templates:Item"] ?? DefaultItemTemplate
        };
    }

    public IReadOnlyList<LabelTemplate> GetTemplates()
    {
        return _templates
            .Select(x => new LabelTemplate
            {
                Type = x.Key,
                ZplTemplate = x.Value
            })
            .OrderBy(x => x.Type)
            .ToList();
    }

    public LabelTemplateType ParseTemplateType(string? templateType)
    {
        if (string.IsNullOrWhiteSpace(templateType))
        {
            throw new InvalidOperationException("TemplateType is required.");
        }

        return templateType.Trim().ToUpperInvariant() switch
        {
            "LOCATION" => LabelTemplateType.Location,
            "HU" => LabelTemplateType.HandlingUnit,
            "HANDLING_UNIT" => LabelTemplateType.HandlingUnit,
            "ITEM" => LabelTemplateType.Item,
            _ => throw new InvalidOperationException($"Unsupported template type '{templateType}'.")
        };
    }

    public string ToApiTemplateType(LabelTemplateType templateType)
    {
        return templateType switch
        {
            LabelTemplateType.Location => "LOCATION",
            LabelTemplateType.HandlingUnit => "HANDLING_UNIT",
            LabelTemplateType.Item => "ITEM",
            _ => throw new InvalidOperationException($"Unsupported template type '{templateType}'.")
        };
    }

    public string Render(string templateType, IReadOnlyDictionary<string, string> placeholders)
    {
        var parsedType = ParseTemplateType(templateType);
        return Render(parsedType, placeholders);
    }

    public string Render(LabelTemplateType templateType, IReadOnlyDictionary<string, string> placeholders)
    {
        if (!_templates.TryGetValue(templateType, out var template))
        {
            throw new InvalidOperationException($"Unsupported template type '{templateType}'.");
        }

        var source = NormalizePlaceholders(placeholders);
        return PlaceholderPattern.Replace(template, match =>
        {
            var key = match.Groups["key"].Value;
            return source.TryGetValue(key, out var value) ? value : string.Empty;
        });
    }

    public LabelPreviewResult BuildPreview(LabelTemplateType templateType, IReadOnlyDictionary<string, string> placeholders)
    {
        var zpl = Render(templateType, placeholders);
        var pdf = BuildSimplePdfBytes(zpl);
        var fileName = templateType switch
        {
            LabelTemplateType.Location => "location-preview.pdf",
            LabelTemplateType.HandlingUnit => "handling-unit-preview.pdf",
            LabelTemplateType.Item => "item-preview.pdf",
            _ => "label-preview.pdf"
        };

        return new LabelPreviewResult(pdf, "application/pdf", fileName);
    }

    public LabelPreviewResult BuildPreview(string templateType, IReadOnlyDictionary<string, string> placeholders)
    {
        var parsedType = ParseTemplateType(templateType);
        return BuildPreview(parsedType, placeholders);
    }

    private static byte[] BuildSimplePdfBytes(string body)
    {
        var lines = body
            .Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Take(70)
            .Select(EscapePdfText)
            .ToArray();

        var contentBuilder = new StringBuilder();
        var currentY = 780;
        foreach (var line in lines)
        {
            contentBuilder.Append(CultureInfo.InvariantCulture, $"BT /F1 10 Tf 40 {currentY} Td ({line}) Tj ET\n");
            currentY -= 12;
        }

        var contentStream = Encoding.ASCII.GetBytes(contentBuilder.ToString());
        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, Encoding.ASCII, leaveOpen: true);

        var offsets = new List<int>();

        void WriteObject(string value)
        {
            writer.Write(value);
            writer.Flush();
        }

        WriteObject("%PDF-1.4\n");

        offsets.Add((int)ms.Position);
        WriteObject("1 0 obj << /Type /Catalog /Pages 2 0 R >> endobj\n");

        offsets.Add((int)ms.Position);
        WriteObject("2 0 obj << /Type /Pages /Kids [3 0 R] /Count 1 >> endobj\n");

        offsets.Add((int)ms.Position);
        WriteObject("3 0 obj << /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >> endobj\n");

        offsets.Add((int)ms.Position);
        WriteObject("4 0 obj << /Type /Font /Subtype /Type1 /BaseFont /Courier >> endobj\n");

        offsets.Add((int)ms.Position);
        WriteObject($"5 0 obj << /Length {contentStream.Length} >> stream\n");
        ms.Write(contentStream, 0, contentStream.Length);
        WriteObject("\nendstream endobj\n");

        var startXref = (int)ms.Position;
        WriteObject("xref\n");
        WriteObject("0 6\n");
        WriteObject("0000000000 65535 f \n");
        foreach (var offset in offsets)
        {
            WriteObject($"{offset:D10} 00000 n \n");
        }

        WriteObject("trailer << /Size 6 /Root 1 0 R >>\n");
        WriteObject($"startxref\n{startXref}\n%%EOF");
        writer.Flush();

        return ms.ToArray();
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    private static Dictionary<string, string> NormalizePlaceholders(IReadOnlyDictionary<string, string> placeholders)
    {
        var source = new Dictionary<string, string>(placeholders, StringComparer.OrdinalIgnoreCase);

        if (!source.ContainsKey("HUBarcode") && source.TryGetValue("Lpn", out var lpn))
        {
            source["HUBarcode"] = lpn;
        }

        if (!source.ContainsKey("ItemSKU") && source.TryGetValue("Sku", out var sku))
        {
            source["ItemSKU"] = sku;
        }

        if (!source.ContainsKey("ItemSKU") && source.TryGetValue("ItemCode", out var itemCode))
        {
            source["ItemSKU"] = itemCode;
        }

        if (!source.ContainsKey("Qty") && source.TryGetValue("Quantity", out var quantity))
        {
            source["Qty"] = quantity;
        }

        if (!source.ContainsKey("Description") && source.TryGetValue("ItemName", out var itemName))
        {
            source["Description"] = itemName;
        }

        return source;
    }

    private const string DefaultLocationTemplate = """
                                                    ^XA
                                                    ^PW812
                                                    ^LL406
                                                    ^FO30,30^A0N,36,36^FDLOCATION^FS
                                                    ^FO30,80^BY2,3,60^BCN,90,Y,N,N^FD{{LocationCode}}^FS
                                                    ^FO30,190^A0N,26,26^FDAisle: {{Aisle}}^FS
                                                    ^FO30,225^A0N,26,26^FDRack: {{Rack}}^FS
                                                    ^FO30,260^A0N,26,26^FDLevel: {{Level}}^FS
                                                    ^FO30,295^A0N,26,26^FDBin: {{Bin}}^FS
                                                    ^XZ
                                                    """;

    private const string DefaultHandlingUnitTemplate = """
                                                        ^XA
                                                        ^PW812
                                                        ^LL1218
                                                        ^FO30,30^A0N,42,42^FDHANDLING UNIT^FS
                                                        ^FO30,90^BY2,3,70^BCN,120,Y,N,N^FD{{HUBarcode}}^FS
                                                        ^FO30,240^A0N,30,30^FDHU: {{HUBarcode}}^FS
                                                        ^FO30,285^A0N,30,30^FDItem: {{ItemSKU}}^FS
                                                        ^FO30,330^A0N,30,30^FDQty: {{Qty}}^FS
                                                        ^FO30,375^A0N,30,30^FDLot: {{LotNumber}}^FS
                                                        ^FO30,420^A0N,30,30^FDExpiry: {{ExpiryDate}}^FS
                                                        ^XZ
                                                        """;

    private const string DefaultItemTemplate = """
                                                ^XA
                                                ^PW406
                                                ^LL203
                                                ^FO20,20^A0N,26,26^FD{{ItemSKU}}^FS
                                                ^FO20,60^BY2,3,45^BCN,80,Y,N,N^FD{{ItemSKU}}^FS
                                                ^FO20,155^A0N,20,20^FD{{Description}}^FS
                                                ^XZ
                                                """;
}
