using System.Text.Json;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.Orders;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class OrderNoteHistoryTests
{
    [Fact]
    public async Task History_is_readonly_encodes_legacy_text_and_preserves_duplicate_entries()
    {
        var entries = new OrderNoteHistoryEntry[] {
            new(1,"<script>alert(1)</script>",new(2026,11,2,10,30,0),"Legacy operator"),
            new(2,"<script>alert(1)</script>",null,null),
            new(3,"Kliento prasymu anuliuotas",null,null) };
        var html = await Render(entries);
        Assert.Contains("3 entries",html);
        Assert.Contains("Legacy operator",html);
        Assert.DoesNotContain("<script>",html);
        Assert.Equal(2,html.Split("&lt;script&gt;").Length-1);
        Assert.Contains("Order cancelled at the customer",html);
        Assert.DoesNotContain("Kliento prasymu anuliuotas",html);
        Assert.DoesNotContain("textarea",html);
        Assert.Equal("Kliento prasymu anuliuotas",entries[2].Note);
    }
    [Fact]
    public async Task Empty_history_does_not_render_a_fake_audit_entry()
        => Assert.Equal("",await Render([]));
    [Fact]
    public void Editable_header_does_not_accept_browser_supplied_history()
        => Assert.Throws<JsonException>(()=>JsonSerializer.Deserialize<OrderHeaderInput>("""
            {"SeriesId":1,"Number":"VV-1-26","Date":"2026-11-02","Due":"2026-11-03",
             "CustomerId":1,"StoreId":1,"Note":"Current note","NoteHistory":[]}
            """));
    private static async Task<string> Render(IReadOnlyList<OrderNoteHistoryEntry> entries)
    {
        await using var services = new ServiceCollection().AddLogging().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services,services.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async()=>
            (await renderer.RenderComponentAsync<OrderNoteHistory>(ParameterView.FromDictionary(
                new Dictionary<string,object?>{{nameof(OrderNoteHistory.Entries),entries}}))).ToHtmlString());
    }
}
