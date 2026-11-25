using LKvitai.MES.BuildingBlocks.WebUI.Components;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public class ShellHelpLinkTests
{
    [Theory]
    [InlineData("https://cikada.lauresta.com/sales/", "/manuals/sales/")]
    [InlineData("https://cikada.lauresta.com/finance/", "/manuals/finance/")]
    [InlineData("https://cikada.lauresta.com/Shopfloor/gamyba", "/manuals/shopfloor/")]
    [InlineData("https://cikada.lauresta.com/frontline/", "/manuals/frontline/")]
    [InlineData("https://cikada.lauresta.com/portal/", "/manuals/")]
    [InlineData("https://cikada.lauresta.com/warehouse/", "/manuals/")]
    [InlineData("https://cikada.lauresta.com/", "/manuals/")]
    [InlineData("not a uri", "/manuals/")]
    [InlineData(null, "/manuals/")]
    public void Help_link_opens_the_manual_section_of_the_current_module(string? baseUri, string expected) =>
        Assert.Equal(expected, PortalModuleShell.BuildHelpUrl(baseUri));
}
