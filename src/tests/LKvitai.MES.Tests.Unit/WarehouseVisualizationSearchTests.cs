using FluentAssertions;
using LKvitai.MES.WebUI.Models;
using LKvitai.MES.WebUI.Services;
using Xunit;

namespace LKvitai.MES.Tests.Unit;

public class WarehouseVisualizationSearchTests
{
    [Fact]
    public void GetSuggestions_WhenBinsEmpty_ShouldReturnEmpty()
    {
        var result = WarehouseVisualizationSearch.GetSuggestions([], "R1");
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetSuggestions_WhenQueryBlank_ShouldReturnEmpty()
    {
        var bins = BuildBins("R1-C1-L1");
        var result = WarehouseVisualizationSearch.GetSuggestions(bins, " ");
        result.Should().BeEmpty();
    }

    [Fact]
    public void GetSuggestions_ShouldReturnExactMatchFirst()
    {
        var bins = BuildBins("R1-C1-L1", "R1-C1-L2", "R1-C1-L10");
        var result = WarehouseVisualizationSearch.GetSuggestions(bins, "R1-C1-L1");
        result.First().Should().Be("R1-C1-L1");
    }

    [Fact]
    public void GetSuggestions_ShouldPrioritizeStartsWithOverContains()
    {
        var bins = BuildBins("A-R1-C1", "R1-C1-L1", "X-R1-C1");
        var result = WarehouseVisualizationSearch.GetSuggestions(bins, "R1-C1");
        result.First().Should().Be("R1-C1-L1");
    }

    [Fact]
    public void GetSuggestions_ShouldMatchCaseInsensitive()
    {
        var bins = BuildBins("r2-c2-l2", "R2-C2-L3");
        var result = WarehouseVisualizationSearch.GetSuggestions(bins, "R2-C2");
        result.Should().Contain("r2-c2-l2");
        result.Should().Contain("R2-C2-L3");
    }

    [Fact]
    public void GetSuggestions_ShouldLimitToMaxResults()
    {
        var bins = Enumerable.Range(1, 20).Select(x => $"R1-C1-L{x}").ToArray();
        var result = WarehouseVisualizationSearch.GetSuggestions(BuildBins(bins), "R1-C1", maxResults: 5);
        result.Should().HaveCount(5);
    }

    [Fact]
    public void GetSuggestions_ShouldDeduplicateCodes()
    {
        var bins = BuildBins("R1-C1-L1", "R1-C1-L1", "R1-C1-L2");
        var result = WarehouseVisualizationSearch.GetSuggestions(bins, "R1-C1");
        result.Count(x => x == "R1-C1-L1").Should().Be(1);
    }

    [Fact]
    public void FindBestMatch_ShouldReturnExactMatch()
    {
        var bins = BuildBins("R1-C1-L1", "R1-C1-L2");
        var result = WarehouseVisualizationSearch.FindBestMatch(bins, "R1-C1-L2");
        result.Should().NotBeNull();
        result!.Code.Should().Be("R1-C1-L2");
    }

    [Fact]
    public void FindBestMatch_ShouldPreferStartsWithOverContains()
    {
        var bins = BuildBins("A-R3-C6", "R3-C6-L3B3");
        var result = WarehouseVisualizationSearch.FindBestMatch(bins, "R3-C6");
        result.Should().NotBeNull();
        result!.Code.Should().Be("R3-C6-L3B3");
    }

    [Fact]
    public void FindBestMatch_WhenNoMatch_ShouldReturnNull()
    {
        var bins = BuildBins("R1-C1-L1", "R1-C1-L2");
        var result = WarehouseVisualizationSearch.FindBestMatch(bins, "ZZZ");
        result.Should().BeNull();
    }

    private static List<VisualizationBinDto> BuildBins(params string[] codes)
    {
        return codes
            .Select((code, idx) => new VisualizationBinDto(
                idx + 1,
                code,
                new VisualizationCoordinateDto(idx, idx, idx),
                new VisualizationBinDimensionsDto(1m, 1m, 1m),
                new VisualizationCapacityDto(100m, 100m),
                25m,
                "LOW",
                "#FFFF00",
                []))
            .ToList();
    }
}
