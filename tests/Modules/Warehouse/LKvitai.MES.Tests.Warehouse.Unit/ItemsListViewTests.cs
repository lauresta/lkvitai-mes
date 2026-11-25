using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure.ItemsList;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public class ItemsListViewTests
{
    [Theory]
    [InlineData("cards", ItemsViewMode.Cards)]
    [InlineData("CARDS", ItemsViewMode.Cards)]
    [InlineData(" table ", ItemsViewMode.Table)]
    public void Parse_KnownTokens_ReturnMode(string value, ItemsViewMode expected)
        => ItemsListView.Parse(value).Should().Be(expected);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("grid")]
    public void Parse_UnknownTokens_ReturnNull(string? value)
    {
        ItemsListView.Parse(value).Should().BeNull();
        ItemsListView.FromQuery(value).Should().Be(ItemsViewMode.Table);
    }

    [Fact]
    public void ToQuery_OnlyCardsHaveAParameter()
    {
        ItemsListView.ToQuery(ItemsViewMode.Cards).Should().Be("cards");
        ItemsListView.ToQuery(ItemsViewMode.Table).Should().BeNull();
        ItemsListView.ToStorage(ItemsViewMode.Table).Should().Be("table");
        ItemsListView.ToStorage(ItemsViewMode.Cards).Should().Be("cards");
    }

    [Theory]
    [InlineData(null, "cards", ItemsViewMode.Cards)]
    [InlineData("", "table", ItemsViewMode.Table)]
    [InlineData("table", "cards", null)]
    [InlineData("cards", "table", null)]
    [InlineData("junk", "cards", ItemsViewMode.Cards)]
    [InlineData(null, null, null)]
    public void RememberedFor_AppliesOnlyWithoutValidQueryValue(string? query, string? stored, ItemsViewMode? expected)
        => ItemsListView.RememberedFor(query, stored).Should().Be(expected);

    [Fact]
    public void OriginalUrl_ReplacesOnlySize_AndKeepsUrlRelative()
    {
        const string guid = "0b7d5c3e-0000-4000-8000-000000000001";
        var original = ItemsListView.OriginalUrl($"api/warehouse/v1/items/42/photos/{guid}?size=thumb");

        original.Should().Be($"api/warehouse/v1/items/42/photos/{guid}?size=original");
        original.Should().NotStartWith("/");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void OriginalUrl_WithoutThumb_IsEmpty(string? thumb)
        => ItemsListView.OriginalUrl(thumb).Should().BeEmpty();

    [Theory]
    [InlineData("Active", "items-card__dot--ok")]
    [InlineData("Obsolete", "items-card__dot--warn")]
    [InlineData("Discontinued", "items-card__dot--slate")]
    [InlineData("anything", "items-card__dot--slate")]
    public void StatusDotClass_MapsStatus(string status, string expected)
        => ItemsListView.StatusDotClass(status).Should().Contain(expected);

    [Fact]
    public void SwitchView_KeepsPageAndPageSize()
    {
        var state = new ItemsListState();
        state.SetPageSize(25);
        state.SetPage(4);

        state.SwitchView(ItemsViewMode.Cards).Should().BeTrue();
        state.SwitchView(ItemsViewMode.Cards).Should().BeFalse();
        state.SwitchView(ItemsViewMode.Table).Should().BeTrue();

        state.Page.Should().Be(4);
        state.PageSize.Should().Be(25);
    }

    [Fact]
    public void SetPageSize_ClampsAndStartsFromFirstPage()
    {
        var state = new ItemsListState();
        state.SetPage(3);

        state.SetPageSize(10_000);

        state.PageSize.Should().Be(ItemsListState.MaxPageSize);
        state.Page.Should().Be(1);
    }

    [Fact]
    public void AdoptGridPaging_ConvertsZeroBasedPage_AndIgnoresEmptyPageSize()
    {
        var state = new ItemsListState();

        state.AdoptGridPaging(2, 0);

        state.Page.Should().Be(3);
        state.PageSize.Should().Be(ItemsListState.DefaultPageSize);
    }

    [Theory]
    [InlineData(0, 50, 1)]
    [InlineData(50, 50, 1)]
    [InlineData(51, 50, 2)]
    [InlineData(1284, 50, 26)]
    [InlineData(10, 0, 1)]
    public void PageCount_RoundsUp(int total, int size, int expected)
        => ItemsListState.PageCount(total, size).Should().Be(expected);

    [Fact]
    public void Toggle_MatchesByIdAcrossInstances()
    {
        var first = new AdminItemDto { Id = 1, Tags = new List<string> { "a" } };
        var reloaded = new AdminItemDto { Id = 1, Tags = new List<string> { "a" } };
        var other = new AdminItemDto { Id = 2 };

        var selected = ItemsSelection.Toggle(new HashSet<AdminItemDto>(), first);
        selected = ItemsSelection.Toggle(selected, other);
        ItemsSelection.Contains(selected, 1).Should().BeTrue();

        selected = ItemsSelection.Toggle(selected, reloaded);

        selected.Select(x => x.Id).Should().BeEquivalentTo(new[] { 2 });
    }

    [Fact]
    public void Reconcile_SwapsInFreshInstances_AndKeepsOffPageOnes()
    {
        var oldOnPage = new AdminItemDto { Id = 1, Tags = new List<string> { "a" } };
        var offPage = new AdminItemDto { Id = 9 };
        var fresh = new AdminItemDto { Id = 1, Tags = new List<string> { "a" } };

        var result = ItemsSelection.Reconcile(new[] { oldOnPage, offPage }, new[] { fresh });

        result.Should().HaveCount(2);
        result.Should().Contain(x => ReferenceEquals(x, fresh));
        result.Should().Contain(x => ReferenceEquals(x, offPage));
    }

    [Fact]
    public void DataGrid_CurrentPage_IsABindableParameter()
    {
        var property = typeof(MudDataGrid<AdminItemDto>).GetProperty(nameof(MudDataGrid<AdminItemDto>.CurrentPage));

        property.Should().NotBeNull();
        property!.GetCustomAttributes(typeof(ParameterAttribute), true).Should().NotBeEmpty();
    }
}
