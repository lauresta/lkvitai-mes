using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

/// <summary>Source-level guards for defects reported at the client demo (issues 362-365).</summary>
public sealed class OrderEntryPickerContractTests
{
    [Fact]
    public void Product_category_changes_on_click_and_keyboard_only_never_on_hover()
    {
        var workspace = Source("src/Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/Components/NewOrderWorkspace.razor");
        Assert.DoesNotContain("onmouseenter", workspace, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("@onclick=\"() => SelectCategory(category.Id)\"", workspace, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ReferenceAutocomplete.razor")]
    [InlineData("MaterialAutocomplete.razor")]
    public void Autocomplete_options_are_chosen_by_record_id_in_the_browser_not_by_a_row_click_handler(string component)
    {
        var source = Source("src/Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/Components/" + component);
        Assert.Contains("data-option-id=", source, StringComparison.Ordinal);
        Assert.Contains("public async Task SelectFromBrowser(int id)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("@onclick=\"@(() => SelectAsync(", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("src/Modules/Sales/LKvitai.MES.Modules.Sales.Infrastructure/Sql/SqlLiveOrderEntryCatalog.cs", "m", "k")]
    [InlineData("src/Modules/Sales/LKvitai.MES.Modules.Sales.Infrastructure/Sql/SqlSalesOrderEntryReferences.cs", "manager", "consultant")]
    public void Series_defaults_never_offer_a_hidden_employee(string file, string manager, string consultant)
    {
        var source = Source(file);
        Assert.Contains($"s.Vad_DarbuotojasID AND ISNULL({manager}.bHideInApplicationFlag,0)=0", source, StringComparison.Ordinal);
        Assert.Contains($"s.Kons_DarbuotojoID AND ISNULL({consultant}.bHideInApplicationFlag,0)=0", source, StringComparison.Ordinal);
    }

    [Fact]
    public void A_product_without_a_chosen_material_shows_the_placeholder_not_the_code_of_component_zero()
    {
        var workspace = Source("src/Modules/Sales/LKvitai.MES.Modules.Sales.WebUI/Components/NewOrderWorkspace.razor.cs");
        Assert.Contains("ColourId=item.ColourId==0 ? (int?)null : item.ColourId, ColourCode=item.ColourId==0 ? \"\" : item.ColourCode ?? \"\"", workspace, StringComparison.Ordinal);
    }

    private static string Source(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "docker-compose.test.yml"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, relativePath));
    }
}
