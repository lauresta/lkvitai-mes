using System.Text.Json;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class LegacyOrderPresentationTests
{
    [Theory]
    [InlineData("Anuliuotas")]
    [InlineData(" ANULIUOTAS ")]
    [InlineData("Atšauktas")]
    public void Native_cancellation_label_and_existing_alias_use_cancelled_presentation(string label)
        => Assert.Equal(OrderStatusCodes.Cancelled, SalesOrderStatusMap.ToStatusCode(label));

    [Theory]
    [InlineData("Kliento prasymu anuliuotas", true)]
    [InlineData(" KLIENTO PRASYMU ANULIUOTAS ", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("Anuliuotas", false)]
    [InlineData("Kliento prasymu", false)]
    [InlineData("Not Kliento prasymu anuliuotas", false)]
    [InlineData("Kliento prasymu anuliuotas; additional note", false)]
    public void Privacy_notice_is_reserved_for_exact_legacy_business_marker(string? note, bool expected)
        => Assert.Equal(expected, LegacyOrderPrivacy.IsRestricted(note));

    [Theory]
    [InlineData("RSR816", "")]
    [InlineData(" rsr816 ", "")]
    [InlineData(null, "")]
    [InlineData("RSR817", "RSR817")]
    [InlineData("R120", "R120")]
    public void Component_zero_code_is_shown_as_no_colour(string? code, string expected)
        => Assert.Equal(expected, LegacyNoColour.Display(code));

    [Fact]
    public void Orders_list_row_defaults_to_no_privacy_flag_and_the_query_projects_it_last()
    {
        var row = JsonSerializer.Deserialize<OrderSummaryDto>("""
            {"Id":1,"Number":"TEST-1-26","Date":"2026-11-02","Price":0,"Debt":0,"IsOverdue":false,
             "Customer":"C","HasDebt":false,"IsVip":false,"HasNote":false,"Status":"Anuliuotas",
             "StatusCode":"cancelled","Store":"S","Address":"A"}
            """)!;
        Assert.False(row.IsPrivacyRestricted);

        using var stream = typeof(SalesOrderStatusMap).Assembly.GetManifestResourceStream(
            "LKvitai.MES.Modules.Sales.Infrastructure.Sql.Queries.SalesOrdersPaged.sql")!;
        var sql = new StreamReader(stream).ReadToEnd();
        var total = sql.LastIndexOf("AS TotalRows", StringComparison.Ordinal);
        var flag = sql.LastIndexOf("AS IsPrivacyRestricted", StringComparison.Ordinal);
        Assert.True(total > 0 && flag > total);
        Assert.Contains("N'Kliento prasymu anuliuotas'", sql);
    }

    [Fact]
    public void Existing_details_payload_defaults_to_no_privacy_notice_and_preserves_prices()
    {
        var details = JsonSerializer.Deserialize<OrderDetailsDto>("""
            {"Id":1,"Number":"TEST-1-26","Date":"2026-11-02","Price":0,"Debt":0,
             "Customer":"Retained customer","Status":"Anuliuotas","StatusCode":"cancelled",
             "Store":"Store","Address":"Retained address","ItemGroups":[],"Amounts":[],"Employees":[]}
            """)!;
        Assert.False(details.IsPrivacyRestricted);
        Assert.Equal(0m, details.Price);
        var notified = details with { IsPrivacyRestricted = true };
        Assert.Equal(details.Customer, notified.Customer);
        Assert.Equal(details.Address, notified.Address);
        Assert.Equal(details.Price, notified.Price);
        Assert.Contains("\"IsPrivacyRestricted\":true", JsonSerializer.Serialize(notified));
    }
}
