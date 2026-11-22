using LKvitai.MES.Modules.Warehouse.Api.Services;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class SupplierItemCodesTests
{
    private static readonly (int SupplierId, string SupplierSku)[] Mappings = [(1, "ACME-100"), (2, "ZX-77"), (2, "ZX-12")];

    [Fact]
    public void The_requested_suppliers_code_wins_over_a_search_match()
        => Assert.Equal("ACME-100", SupplierItemCodes.Pick(Mappings, 1, "zx"));

    [Fact]
    public void Without_a_supplier_the_code_that_matched_the_search_is_shown_exact_match_first()
    {
        Assert.Equal("ZX-12", SupplierItemCodes.Pick(Mappings, null, "zx"));
        Assert.Equal("ZX-77", SupplierItemCodes.Pick(Mappings, null, "zx-77"));
    }

    [Fact]
    public void A_supplier_without_a_code_for_the_item_falls_back_to_the_search_match()
        => Assert.Equal("ACME-100", SupplierItemCodes.Pick(Mappings, 9, "acme"));

    [Fact]
    public void No_code_is_guessed_when_nothing_matches()
    {
        Assert.Null(SupplierItemCodes.Pick(Mappings, null, null));
        Assert.Null(SupplierItemCodes.Pick(Mappings, 9, "rm-0001"));
        Assert.Null(SupplierItemCodes.Pick([], 1, "acme"));
        Assert.Null(SupplierItemCodes.Pick([(1, " ")], 1, null));
    }
}
