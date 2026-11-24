using LKvitai.MES.Modules.Finance.Api.BankPayments;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LKvitai.MES.Tests.Finance.Unit;

public class BankPaymentWritesTests
{
    private static IConfiguration Config(string? value) => new ConfigurationBuilder()
        .AddInMemoryCollection(value is null ? [] : new Dictionary<string, string?> { [BankPaymentWrites.ConfigKey] = value })
        .Build();

    [Fact]
    public void Writes_are_off_unless_the_stand_opts_in()
    {
        Assert.False(BankPaymentWrites.Enabled(Config(null)));
        Assert.False(BankPaymentWrites.Enabled(Config("false")));
        Assert.True(BankPaymentWrites.Enabled(Config("true")));
    }

    [Theory]
    [InlineData("POST", "/api/finance/bank-payments", true)]
    [InlineData("POST", "/api/finance/bank-payments/", true)]
    [InlineData("DELETE", "/api/finance/bank-payments/201113960", true)]
    [InlineData("POST", "/api/finance/bank-payments/parse", false)]
    [InlineData("POST", "/api/finance/bank-payments/preview", false)]
    [InlineData("GET", "/api/finance/bank-payments", false)]
    public void Only_import_and_deletion_count_as_writes(string method, string path, bool expected) =>
        Assert.Equal(expected, BankPaymentWrites.IsWrite(method, path));

    [Fact]
    public void A_page_reports_writes_enabled_unless_told_otherwise()
    {
        var page = new BankPaymentPage([], 0, 0, null, 1, 100);
        Assert.True(page.WritesEnabled);
        Assert.False((page with { WritesEnabled = false }).WritesEnabled);
    }
}
