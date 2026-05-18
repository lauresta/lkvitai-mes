using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using System.Text.Json;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class AgnumProductDtoDeserializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true
    };

    [Fact]
    public void Deserialize_ProductWithSingleBarcodeString_ShouldPopulateBarcodes()
    {
        var json = "{\"id\":1,\"code\":\"SKU-001\",\"name\":\"Test\",\"pcs\":\"vnt\",\"enabled\":true,\"balance\":10,\"barcode\":\"12345\"}";

        var result = JsonSerializer.Deserialize<AgnumProductDto>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Barcode.Should().Be("12345");
        result.Barcodes.Should().BeNull();
    }

    [Fact]
    public void Deserialize_ProductWithBarcodesArray_ShouldPopulateBarcodesList()
    {
        var json = "{\"id\":1,\"code\":\"SKU-001\",\"name\":\"Test\",\"pcs\":\"vnt\",\"enabled\":true,\"balance\":10,\"barcodes\":[\"12345\",\"67890\"]}";

        var result = JsonSerializer.Deserialize<AgnumProductDto>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Barcode.Should().BeNull();
        result.Barcodes.Should().ContainInOrder("12345", "67890");
    }

    [Fact]
    public void Deserialize_ProductWithBothBarcodeFields_ShouldPopulateBothWithoutError()
    {
        var json = "{\"id\":1,\"code\":\"SKU-001\",\"name\":\"Test\",\"pcs\":\"vnt\",\"enabled\":true,\"balance\":10,\"barcode\":\"12345\",\"barcodes\":[\"67890\"]}";

        var result = JsonSerializer.Deserialize<AgnumProductDto>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Barcode.Should().Be("12345");
        result.Barcodes.Should().ContainInOrder("67890");
    }

    [Fact]
    public void Deserialize_ProductWithNoBarcodeFields_ShouldNotFail()
    {
        var json = "{\"id\":1,\"code\":\"SKU-001\",\"name\":\"Test\",\"pcs\":\"vnt\",\"enabled\":true,\"balance\":10}";

        var result = JsonSerializer.Deserialize<AgnumProductDto>(json, JsonOptions);

        result.Should().NotBeNull();
        result!.Barcode.Should().BeNull();
        result.Barcodes.Should().BeNull();
    }
}
