using System.Text.RegularExpressions;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

/// <summary>
/// The test stand and production share one legacy database. The test stand must stay
/// inside the TESTAVIMAS branch; only production may write to every branch.
/// </summary>
public sealed class LegacyWriteScopeContractTests
{
    [Fact]
    public void Default_scope_is_the_test_branch_and_its_configured_series()
    {
        var options = new SalesOrderEntryOptions { AllowedSeriesIds = [768439496] };

        Assert.False(options.AllBranches);
        Assert.True(options.InScope(SalesOrderEntryOptions.TestBranchId, 768439496));
        Assert.False(options.InScope(SalesOrderEntryOptions.TestBranchId, 1));
        Assert.False(options.InScope(1, 768439496));
        Assert.False(options.InScope(null, 768439496));
    }

    [Fact]
    public void Default_scope_without_series_is_not_configured()
    {
        Assert.False(new SalesOrderEntryOptions().ScopeConfigured);
        Assert.True(new SalesOrderEntryOptions { AllBranches = true }.ScopeConfigured);
    }

    [Fact]
    public void All_branches_scope_accepts_any_branch_and_series()
    {
        var options = new SalesOrderEntryOptions { AllBranches = true };

        Assert.True(options.InScope(1, 12345));
        Assert.True(options.InScope(SalesOrderEntryOptions.TestBranchId, 768439496));
    }

    [Fact]
    public void Test_stand_never_switches_on_all_branches()
    {
        var compose = Read("docker-compose.test.yml");

        Assert.DoesNotContain("AllBranches", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("ALL_BRANCHES", compose, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_writes_to_every_branch_in_sales_and_shopfloor()
    {
        var compose = Read("docker-compose.prod.yml");

        Assert.Equal(2, Regex.Matches(compose, @"Sales__OrderEntry__AllBranches: ""\$\{SALES_ORDER_ENTRY_ALL_BRANCHES:-true\}""").Count);
        Assert.Equal(2, Regex.Matches(compose, @"Shopfloor__ProductionTaskWrite__AllBranches: ""\$\{SHOPFLOOR_TASK_WRITE_ALL_BRANCHES:-true\}""").Count);
        Assert.Contains(@"Sales__OrderEntry__Enabled: ""${SALES_ORDER_ENTRY_ENABLED:-true}""", compose, StringComparison.Ordinal);
        // Production must not mark its documents with the test stand's prefix.
        Assert.DoesNotContain("MEST", compose, StringComparison.Ordinal);
    }

    private static string Read(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, file))) directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, file));
    }
}
