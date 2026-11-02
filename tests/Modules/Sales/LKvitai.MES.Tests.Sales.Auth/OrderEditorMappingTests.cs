using System.Reflection;
using System.Text.Json;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using Xunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class OrderEditorMappingTests
{
    [Fact]
    public void Confirmation_selection_uses_persisted_manufactured_facts_and_retains_historical_types()
    {
        var component=new NewOrderWorkspace();
        SalesTestLocalization.Wire(component);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=typeof(NewOrderWorkspace);
        var catalogue=new DemoCatalog { Categories=[
            new DemoCategory {Id=1,Active=true,Types=[new DemoType{Id=1,IsManufactured=true},new DemoType{Id=2,IsManufactured=false},new DemoType{Id=3,IsManufactured=true,UnsupportedReason="Unsupported"}]},
            new DemoCategory {Id=2,Active=false,Types=[new DemoType{Id=4,IsManufactured=true}]}] };
        var order=new DemoOrderDraft { ConfirmationEnabled=true,WritesAllowed=true };
        order.Lines.AddRange([
            new DemoOrderLine{LegacyId=100,CategoryId=1,TypeId=1,StateId=4,Quantity=1},
            new DemoOrderLine{LegacyId=101,CategoryId=1,TypeId=1,StateId=7,Quantity=1},
            new DemoOrderLine{LegacyId=102,CategoryId=1,TypeId=1,StateId=8,Quantity=1},
            new DemoOrderLine{LegacyId=103,CategoryId=1,TypeId=2,StateId=4,Quantity=1},
            new DemoOrderLine{LegacyId=104,CategoryId=1,TypeId=3,StateId=4,Quantity=1},
            new DemoOrderLine{LegacyId=105,CategoryId=2,TypeId=4,StateId=4,Quantity=1},
            new DemoOrderLine{CategoryId=1,TypeId=1,StateId=4,Quantity=1},
            new DemoOrderLine{LegacyId=106,CategoryId=1,TypeId=1,StateId=4,Quantity=0},
            new DemoOrderLine{LegacyId=107,CategoryId=1,TypeId=1,StateId=4,Quantity=1},
            new DemoOrderLine{LegacyId=108,CategoryId=1,TypeId=1,StateId=4,Quantity=1},
            new DemoOrderLine{LegacyId=109,CategoryId=1,TypeId=1,StateId=4,Quantity=1}]);
        foreach(var line in order.Lines){line.IsManufactured=true;line.HasStoredPrice=true;line.HasStoredDimensions=true;}
        order.Lines.Single(x=>x.LegacyId==103).IsManufactured=false;
        order.Lines.Single(x=>x.LegacyId==107).HasStoredPrice=false;
        order.Lines.Single(x=>x.LegacyId==108).HasStoredDimensions=false;
        order.Lines.Single(x=>x.LegacyId==109).IsManufactured=null;
        type.GetField("Catalog",flags)!.SetValue(component,catalogue);
        type.GetField("_order",flags)!.SetValue(component,order);
        var selected=(IReadOnlyList<DemoOrderLine>)type.GetProperty("ConfirmationCandidates",flags)!.GetValue(component)!;
        Assert.Equal(new long?[]{100,101,104,105},selected.Select(line=>line.LegacyId));
        Assert.True((bool)type.GetProperty("CanConfirmLiveProducts",flags)!.GetValue(component)!);
        order.ConfirmationEnabled=false;
        Assert.False((bool)type.GetProperty("CanConfirmLiveProducts",flags)!.GetValue(component)!);
    }

    [Fact]
    public void Payment_capability_is_independent_from_header_edit_and_history_keeps_real_hidden_receiver_labels()
    {
        var snapshot=JsonSerializer.Deserialize<OrderEditorSnapshot>("""
            {"Header":{"Id":1,"Number":"XXXX-1-26","SeriesId":2,"BranchId":7,
             "Date":"2026-10-31","Due":"2026-11-14","CustomerId":1,"StoreId":2,
             "StateId":4,"CanEdit":false,"CalculatedTotal":10,"Paid":1.25},
             "Products":[],"Duties":[],"Revision":"r2","WritesAllowed":true,"Warnings":[],
             "PaymentsEnabled":true,"AllowedPaymentMethods":[81423987],
             "Payments":[{"Id":987,"Date":"2026-10-31","Amount":1.25,"MethodId":81423987,
               "EmployeeId":123,"CurrencyId":1,"CurrencyCode":"LTL",
               "MethodName":"Actual legacy cash method","EmployeeName":"Approved hidden receiver"}]}
            """)!;
        var component=new NewOrderWorkspace();
        SalesTestLocalization.Wire(component);
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;
        var type=typeof(NewOrderWorkspace);
        void Apply(OrderEditorSnapshot data)=>type.GetMethod("ApplySnapshot",flags)!.Invoke(component,[data,null,false,false]);
        Apply(snapshot);
        var model=(DemoOrderDraft)type.GetField("_order",flags)!.GetValue(component)!;
        Assert.False(model.CanEdit);
        Assert.True((bool)type.GetProperty("CanPostLivePayment",flags)!.GetValue(component)!);
        var payment=Assert.Single(model.Payments);
        Assert.Equal("Actual legacy cash method",payment.Method);
        Assert.Equal("Approved hidden receiver",payment.ReceiverOrRegister);
        Assert.Equal("LTL",payment.CurrencyCode);
        foreach(var denied in new[]{snapshot with{PaymentsEnabled=false},snapshot with{WritesAllowed=false},snapshot with{Header=snapshot.Header with{Cancelled=true}}})
        {
            Apply(denied);
            Assert.False((bool)type.GetProperty("CanPostLivePayment",flags)!.GetValue(component)!);
        }
    }

    [Fact]
    public async Task Historical_type_renders_its_saved_selection_without_adding_it_to_new_product_choices()
    {
        var activeTypes = new List<DemoType> { new() { Id=1, Name="Current active type" } };
        await using var services = new ServiceCollection().AddLogging().AddLocalization().BuildServiceProvider();
        await using var renderer = new HtmlRenderer(services, services.GetRequiredService<ILoggerFactory>());
        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<ProductTypeSelect>(ParameterView.FromDictionary(new Dictionary<string,object?>
            { [nameof(ProductTypeSelect.Value)]=-42, [nameof(ProductTypeSelect.SavedName)]="Historical R25", [nameof(ProductTypeSelect.Types)]=activeTypes }));
            return output.ToHtmlString();
        });
        Assert.Contains("value=\"-42\"", html);
        Assert.True(System.Text.RegularExpressions.Regex.IsMatch(html, "<option(?=[^>]*value=\"-42\")(?=[^>]*selected)[^>]*>Historical R25 \\(saved\\)</option>"), html);
        Assert.Contains("<option value=\"1\">Current active type</option>", html);
        var component = new NewOrderWorkspace();
        SalesTestLocalization.Wire(component);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(NewOrderWorkspace).GetField("Catalog", flags)!.SetValue(component, new DemoCatalog { Categories=[new DemoCategory { Id=7, Name="Roletai", Active=true, Types=activeTypes }] });
        typeof(NewOrderWorkspace).GetField("_selectedCategoryId", flags)!.SetValue(component, 7);
        var choices = (IReadOnlyList<DemoType>)typeof(NewOrderWorkspace).GetProperty("FilteredTypes", flags)!.GetValue(component)!;
        Assert.Equal(1, Assert.Single(choices).Id);
    }

    [Fact]
    public void Inactive_categories_are_excluded_from_live_new_choices_but_retained_for_saved_product_display()
    {
        var component = new NewOrderWorkspace();
        SalesTestLocalization.Wire(component);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(NewOrderWorkspace);
        type.GetField("Catalog", flags)!.SetValue(component, new DemoCatalog { Categories = [
            new DemoCategory { Id=1, Name="Current", Active=true },
            new DemoCategory { Id=2, Name="Historical", Active=false }] });
        type.GetField("_configuringLine", flags)!.SetValue(component, new DemoOrderLine { CategoryId=2 });
        Assert.Single((IReadOnlyList<DemoCategory>)type.GetProperty("FilteredCategories", flags)!.GetValue(component)!);
        Assert.Equal(2, ((DemoCategory)type.GetProperty("ConfiguredCategory", flags)!.GetValue(component)!).Id);
        type.GetField("IsLive", flags)!.SetValue(component, false);
        Assert.Equal(2, ((IReadOnlyList<DemoCategory>)type.GetProperty("FilteredCategories", flags)!.GetValue(component)!).Count);
    }

    [Fact]
    public void Price_lookup_context_uses_committed_date_while_header_edit_is_buffered()
    {
        var component = new NewOrderWorkspace();
        SalesTestLocalization.Wire(component);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var type = typeof(NewOrderWorkspace);
        type.GetField("_order", flags)!.SetValue(component, new DemoOrderDraft { Date = new DateOnly(2026, 11, 1) });
        type.GetField("_headerSaved", flags)!.SetValue(component, JsonSerializer.Serialize(new OrderHeaderInput(1,"XXXX-1-26",new DateOnly(2026,10,31),new DateOnly(2026,11,15),1,2,null,null)));
        Assert.Equal(new DateOnly(2026,10,31), (DateOnly)type.GetProperty("SavedOrderDate", flags)!.GetValue(component)!);
    }

    [Fact]
    public void Calculation_reference_refresh_does_not_dirty_product_and_note_only_is_separate_from_business_configuration()
    {
        var component = new NewOrderWorkspace();
        SalesTestLocalization.Wire(component);
        var line = new DemoOrderLine { TypeId = 4, Price = 12, WidthMm = 800, HeightMm = 1500, Quantity = 1 };
        var type = typeof(NewOrderWorkspace);
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        type.GetField("_configuringLine", flags)!.SetValue(component, line);
        type.GetField("_productSaved", flags)!.SetValue(component, type.GetMethod("ProductState", flags)!.Invoke(component, [line]));
        line.CalculationRevision = "server-issued-reference";
        line.CalculationContext = "1:3:4:0";
        Assert.False((bool)type.GetProperty("ProductDirty", flags)!.GetValue(component)!);
        line.Note = "Window in test room";
        Assert.True((bool)type.GetProperty("ProductDirty", flags)!.GetValue(component)!);
        Assert.False((bool)type.GetMethod("BusinessConfigurationDirty", flags)!.Invoke(component, [line])!);
        line.Price = 13;
        Assert.True((bool)type.GetMethod("BusinessConfigurationDirty", flags)!.Invoke(component, [line])!);
    }

    [Theory]
    [InlineData(true,true,false,false)]
    [InlineData(false,true,true,true)]
    [InlineData(false,false,true,false)]
    public void Cancellation_and_individual_line_state_do_not_unlock_core_fields(bool cancelled,bool lineFlag,bool expectedHeader,bool expectedLine)
    {
        var snapshot=JsonSerializer.Deserialize<OrderEditorSnapshot>("""
            {"Header":{"Id":1,"Number":"XXXX-1-26","SeriesId":2,"BranchId":7,
             "Date":"2026-10-31","Due":"2026-11-14","CustomerId":1,"StoreId":2,
             "PriorityId":2,"StateId":4,"CanEdit":true,"CalculatedTotal":0,"Paid":0},
             "Products":[{"Id":3,"TypeId":4,"CategoryId":5,"TypeName":"Product",
             "CategoryName":"Category","Quantity":1,"ColourId":0,"Parameters":[],
             "StateId":4,"CanEdit":true,"CanDelete":true,"Accessories":[]}],
             "Duties":[],"Revision":"r1","WritesAllowed":true,"Warnings":[]}
            """)!;
        snapshot=snapshot with {Header=snapshot.Header with {Cancelled=cancelled},Products=[snapshot.Products[0] with {CanEdit=lineFlag,CanDelete=lineFlag}]};
        var component=new NewOrderWorkspace();
        SalesTestLocalization.Wire(component);
        typeof(NewOrderWorkspace).GetMethod("ApplySnapshot",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(component,[snapshot,null,false,false]);
        var model=(DemoOrderDraft)typeof(NewOrderWorkspace).GetField("_order",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(component)!;
        Assert.Equal(expectedHeader,model.CanEdit);
        Assert.Equal(expectedLine,model.Lines.Single().CanEdit);
        Assert.Equal(expectedLine,model.Lines.Single().CanDelete);
        // Scope remains independent so permitted store/due/note operations can
        // retain their separate server-side policy on a cancelled order.
        Assert.True(model.WritesAllowed);
    }
}
