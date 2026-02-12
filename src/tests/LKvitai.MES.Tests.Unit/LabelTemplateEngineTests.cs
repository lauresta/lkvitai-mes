using FluentAssertions;
using LKvitai.MES.Api.Services;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class LabelTemplateEngineTests
{
    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void ParseTemplateType_ShouldSupportLocation()
    {
        var sut = CreateSut();

        var result = sut.ParseTemplateType("LOCATION");

        result.Should().Be(LabelTemplateType.Location);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void ParseTemplateType_ShouldSupportHandlingUnitAlias()
    {
        var sut = CreateSut();

        var result = sut.ParseTemplateType("HU");

        result.Should().Be(LabelTemplateType.HandlingUnit);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void ParseTemplateType_ShouldSupportHandlingUnitLongName()
    {
        var sut = CreateSut();

        var result = sut.ParseTemplateType("HANDLING_UNIT");

        result.Should().Be(LabelTemplateType.HandlingUnit);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void ParseTemplateType_WhenUnknown_ShouldThrow()
    {
        var sut = CreateSut();

        var action = () => sut.ParseTemplateType("UNKNOWN");

        action.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void GetTemplates_ShouldReturnThreeTemplates()
    {
        var sut = CreateSut();

        var templates = sut.GetTemplates();

        templates.Should().HaveCount(3);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void GetTemplates_ShouldContainAllExpectedTypes()
    {
        var sut = CreateSut();

        var templates = sut.GetTemplates();

        templates.Select(x => x.Type).Should().BeEquivalentTo(
            [LabelTemplateType.Location, LabelTemplateType.HandlingUnit, LabelTemplateType.Item]);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void RenderLocation_ShouldReplacePlaceholderValues()
    {
        var sut = CreateSut();

        var zpl = sut.Render(LabelTemplateType.Location, new Dictionary<string, string>
        {
            ["LocationCode"] = "R3-C6-L3B3",
            ["Aisle"] = "R3",
            ["Rack"] = "C6",
            ["Level"] = "L3",
            ["Bin"] = "B3"
        });

        zpl.Should().Contain("R3-C6-L3B3");
        zpl.Should().Contain("Aisle: R3");
        zpl.Should().Contain("Rack: C6");
        zpl.Should().Contain("Level: L3");
        zpl.Should().Contain("Bin: B3");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void RenderLocation_ShouldContainCode128BarcodeCommand()
    {
        var sut = CreateSut();

        var zpl = sut.Render("LOCATION", new Dictionary<string, string>
        {
            ["LocationCode"] = "R1-C1-L1"
        });

        zpl.Should().Contain("^BC");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void Render_WhenPlaceholderMissing_ShouldOutputEmptyValue()
    {
        var sut = CreateSut();

        var zpl = sut.Render("ITEM", new Dictionary<string, string>
        {
            ["ItemSKU"] = "RM-0001"
        });

        zpl.Should().Contain("RM-0001");
        zpl.Should().NotContain("{{");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void Render_ShouldTreatPlaceholderKeysCaseInsensitive()
    {
        var sut = CreateSut();

        var zpl = sut.Render("ITEM", new Dictionary<string, string>
        {
            ["itemsku"] = "RM-1000",
            ["description"] = "Bolt M8"
        });

        zpl.Should().Contain("RM-1000");
        zpl.Should().Contain("Bolt M8");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void RenderHandlingUnit_ShouldIncludeAllHuFields()
    {
        var sut = CreateSut();

        var zpl = sut.Render("HANDLING_UNIT", new Dictionary<string, string>
        {
            ["HUBarcode"] = "HU-001",
            ["ItemSKU"] = "RM-0001",
            ["Qty"] = "50",
            ["LotNumber"] = "LOT-2026-001",
            ["ExpiryDate"] = "2027-02-12"
        });

        zpl.Should().Contain("HU-001");
        zpl.Should().Contain("Item: RM-0001");
        zpl.Should().Contain("Qty: 50");
        zpl.Should().Contain("Lot: LOT-2026-001");
        zpl.Should().Contain("Expiry: 2027-02-12");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void RenderHandlingUnit_ShouldMapLegacyKeys()
    {
        var sut = CreateSut();

        var zpl = sut.Render("HANDLING_UNIT", new Dictionary<string, string>
        {
            ["Lpn"] = "HU-LEGACY",
            ["Sku"] = "RM-LEGACY",
            ["Quantity"] = "9"
        });

        zpl.Should().Contain("HU-LEGACY");
        zpl.Should().Contain("Item: RM-LEGACY");
        zpl.Should().Contain("Qty: 9");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void RenderItem_ShouldIncludeSkuBarcodeAndDescription()
    {
        var sut = CreateSut();

        var zpl = sut.Render("ITEM", new Dictionary<string, string>
        {
            ["ItemSKU"] = "FG-0001",
            ["Description"] = "Widget A"
        });

        zpl.Should().Contain("FG-0001");
        zpl.Should().Contain("Widget A");
        zpl.Should().Contain("^BC");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void RenderItem_ShouldMapLegacyItemNameToDescription()
    {
        var sut = CreateSut();

        var zpl = sut.Render("ITEM", new Dictionary<string, string>
        {
            ["ItemCode"] = "FG-ALT",
            ["ItemName"] = "Widget Legacy"
        });

        zpl.Should().Contain("FG-ALT");
        zpl.Should().Contain("Widget Legacy");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void RenderLocation_ShouldKeepPayloadBelowOneKilobyte()
    {
        var sut = CreateSut();

        var zpl = sut.Render("LOCATION", new Dictionary<string, string>
        {
            ["LocationCode"] = "R1-C1-L1",
            ["Aisle"] = "R1",
            ["Rack"] = "C1",
            ["Level"] = "L1",
            ["Bin"] = "B1"
        });

        zpl.Length.Should().BeLessThan(1024);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void RenderHandlingUnit_ShouldKeepPayloadBelowTwoKilobytes()
    {
        var sut = CreateSut();

        var zpl = sut.Render("HANDLING_UNIT", new Dictionary<string, string>
        {
            ["HUBarcode"] = "HU-001",
            ["ItemSKU"] = "RM-0001",
            ["Qty"] = "50",
            ["LotNumber"] = "LOT-2026-001",
            ["ExpiryDate"] = "2027-02-12"
        });

        zpl.Length.Should().BeLessThan(2048);
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void BuildPreview_ShouldReturnPdfBytes()
    {
        var sut = CreateSut();

        var preview = sut.BuildPreview("LOCATION", new Dictionary<string, string>
        {
            ["LocationCode"] = "R1-C1-L1"
        });

        preview.ContentType.Should().Be("application/pdf");
        preview.Content.Length.Should().BeGreaterThan(64);
        preview.Content[0].Should().Be((byte)'%');
        preview.Content[1].Should().Be((byte)'P');
        preview.Content[2].Should().Be((byte)'D');
        preview.Content[3].Should().Be((byte)'F');
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void BuildPreview_Location_ShouldUseLocationFileName()
    {
        var sut = CreateSut();

        var preview = sut.BuildPreview(LabelTemplateType.Location, new Dictionary<string, string>());

        preview.FileName.Should().Be("location-preview.pdf");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void BuildPreview_HandlingUnit_ShouldUseHuFileName()
    {
        var sut = CreateSut();

        var preview = sut.BuildPreview(LabelTemplateType.HandlingUnit, new Dictionary<string, string>());

        preview.FileName.Should().Be("handling-unit-preview.pdf");
    }

    [Fact]
    [Trait("Category", "LabelPrinting")]
    public void BuildPreview_Item_ShouldUseItemFileName()
    {
        var sut = CreateSut();

        var preview = sut.BuildPreview(LabelTemplateType.Item, new Dictionary<string, string>());

        preview.FileName.Should().Be("item-preview.pdf");
    }

    private static LabelTemplateEngine CreateSut()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
        return new LabelTemplateEngine(configuration);
    }
}
