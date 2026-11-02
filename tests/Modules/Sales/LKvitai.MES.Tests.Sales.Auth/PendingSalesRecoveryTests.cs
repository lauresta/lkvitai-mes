using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class PendingSalesRecoveryTests
{
    private static readonly Guid Actor=Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb");
    [Fact]
    public async Task Created_header_navigates_without_rendering_a_transient_editor_in_the_old_dialog()
    {
        var client=Client(new Storage(),new Transport{FailFirst=false});client.SetOrderScope(null);
        var navigation=new Navigation();var component=new NewOrderWorkspace{Entry=client,Navigation=navigation};
        var flags=BindingFlags.Instance|BindingFlags.NonPublic;var type=typeof(NewOrderWorkspace);
        void Set(string name,object? value)=>type.GetField(name,flags)!.SetValue(component,value);
        Set("Catalog",new DemoCatalog{Series=[new DemoSeries{Id=1,Code="XXXX"}]});
        Set("_rawNumber","XXXX-100");Set("_customerId",30);Set("_storeId",40);
        Set("_date","2026-11-01");Set("_due","2026-11-15");
        await (Task)type.GetMethod("CreateLiveOrderAsync",flags)!.Invoke(component,[])!;
        Assert.Equal("https://test.invalid/sales/entry/42",navigation.Target);
        Assert.Null(type.GetField("_order",flags)!.GetValue(component));
        Assert.True((bool)type.GetField("_busy",flags)!.GetValue(component)!);
        Assert.True((bool)type.GetField("_leaving",flags)!.GetValue(component)!);
    }
    [Fact]
    public async Task Reload_missing_receipt_replays_exact_original_body_and_caller_array_mutation_is_ignored()
    {
        var storage=new Storage();var transport=new Transport();var first=Client(storage,transport);
        first.SetOrderScope(1);
        string?[] parameters=["original"];
        var command=new ConfigureProductCommand(Guid.NewGuid(),"r1",new(42,800,1500,1,0,null,12,false,null,parameters));
        await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>first.ConfigureAsync(1,4,command));
        parameters[0]="changed after sending";
        var restored=Client(storage,transport);restored.SetOrderScope(1);await restored.RestorePendingAsync();
        Assert.Equal(command.CommandId,restored.PendingCommandId);
        Assert.Equal(404,(await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>restored.OutcomeAsync(command.CommandId))).Status);
        Assert.True(restored.HasPendingCommand);
        await restored.RetryPendingAsync();
        Assert.Equal(transport.Bodies[0],transport.Bodies[1]);
        Assert.Contains("original",transport.Bodies[1]);Assert.DoesNotContain("changed after",transport.Bodies[1]);
        Assert.False(restored.HasPendingCommand);
        Assert.DoesNotContain(storage.Values.Values,value=>value.Contains(command.CommandId.ToString()));
    }
    [Fact]
    public async Task Another_order_remains_editable_and_does_not_discard_unknown_original()
    {
        var storage=new Storage();var transport=new Transport();var client=Client(storage,transport);
        client.SetOrderScope(1);var command=new CreateProductCommand(Guid.NewGuid(),"r1",17);
        await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.CreateProductAsync(1,command));
        client.SetOrderScope(2);Assert.False(client.HasPendingCommand);
        Assert.Equal(1,Assert.Single(client.OtherPendingSaves).OrderId);
        await client.CreateProductAsync(2,new(Guid.NewGuid(),"r2",18));
        client.SetOrderScope(1);Assert.Equal(command.CommandId,client.PendingCommandId);
        await client.RetryPendingAsync();
        Assert.Equal(transport.Bodies[0],transport.Bodies[2]);
    }
    [Fact]
    public async Task Other_account_does_not_restore_or_replay_original_accounts_input()
    {
        var storage=new Storage();var transport=new Transport();var client=Client(storage,transport);
        client.SetOrderScope(1);var command=new CreateProductCommand(Guid.NewGuid(),"r1",17);
        await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.CreateProductAsync(1,command));
        var other=Client(storage,transport,Guid.Parse("bbbbbbbb-1111-2222-3333-cccccccccccc"));other.SetOrderScope(1);await other.RestorePendingAsync();
        Assert.False(other.HasPendingCommand);Assert.Empty(other.OtherPendingSaves);
        Assert.Contains(command.CommandId.ToString(),storage.Values[SalesOrderEntryClient.OutboxStorageKey(Actor)]);
    }
    [Fact]
    public async Task Legacy_bare_id_does_not_block_an_unrelated_order_or_disappear()
    {
        var id=Guid.NewGuid();var storage=new Storage();storage.Values["sales-entry-pending"]=id.ToString();
        var transport=new Transport{FailFirst=false};var client=Client(storage,transport);client.SetOrderScope(2);await client.RestorePendingAsync();
        Assert.Equal(id,client.LegacyPendingId);Assert.False(client.HasPendingCommand);
        await client.CreateProductAsync(2,new(Guid.NewGuid(),"r1",17));
        Assert.Equal(id.ToString(),storage.Values["sales-entry-pending"]);
    }
    [Theory]
    [InlineData(true,false)]
    [InlineData(false,true)]
    public async Task Initial_storage_read_or_write_failure_prevents_any_mutation(bool readFailure,bool writeFailure)
    {
        var storage=new Storage{FailRead=readFailure,FailWrite=writeFailure};var transport=new Transport{FailFirst=false};var client=Client(storage,transport);client.SetOrderScope(1);
        var error=await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.CreateProductAsync(1,new(Guid.NewGuid(),"r1",17)));
        Assert.False(error.OutcomeUnknown);Assert.Empty(transport.Bodies);Assert.False(client.HasPendingCommand);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("https://example.invalid/steal")]
    [InlineData("/api/sales/order-entry/orders/1/payments?redirect=external")]
    public async Task Malformed_or_external_route_is_preserved_without_being_restored_or_sent(string? path)
    {
        var storage=new Storage();var transport=new Transport();var client=Client(storage,transport);client.SetOrderScope(1);
        var id=Guid.NewGuid();await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.CreateProductAsync(1,new(id,"r1",17)));
        using var document=JsonDocument.Parse(storage.Values[SalesOrderEntryClient.OutboxStorageKey(Actor)]);
        var stored=document.RootElement[0];
        var malformed=new{version=2,actorId=Actor,id,scope="order:1",orderId=1,method="POST",path,body=stored.GetProperty("body").Clone(),createdAt=DateTimeOffset.UtcNow};
        storage.Values[SalesOrderEntryClient.OutboxStorageKey(Actor)]=JsonSerializer.Serialize(new[]{malformed});
        var restored=Client(storage,transport);restored.SetOrderScope(1);await restored.RestorePendingAsync();
        Assert.False(restored.HasPendingCommand);Assert.NotNull(restored.PendingRestoreWarning);
        Assert.Contains(id.ToString(),storage.Values[SalesOrderEntryClient.OutboxStorageKey(Actor)]);
        Assert.Single(transport.Bodies);
    }
    [Fact]
    public async Task Success_for_wrong_order_does_not_clear_original_input()
    {
        var storage=new Storage();var transport=new Transport();var client=Client(storage,transport);client.SetOrderScope(1);
        var command=new CreateProductCommand(Guid.NewGuid(),"r1",17);await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.CreateProductAsync(1,command));
        transport.WrongResultOrder=2;
        var error=await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.RetryPendingAsync());
        Assert.True(error.OutcomeUnknown);Assert.Equal("RECOVERY_SCOPE_MISMATCH",error.Code);Assert.Equal(command.CommandId,client.PendingCommandId);
    }
    [Fact]
    public async Task Receipt_for_wrong_order_is_not_applied_or_cleared_by_workspace_recovery()
    {
        var storage=new Storage();var transport=new Transport();var client=Client(storage,transport);client.SetOrderScope(1);
        var command=new CreateProductCommand(Guid.NewGuid(),"r1",17);await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.CreateProductAsync(1,command));
        transport.OutcomeOrder=2;
        var component=new NewOrderWorkspace{Entry=client};
        typeof(NewOrderWorkspace).GetField("_order",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(component,new DemoOrderDraft{LegacyId=1});
        await (Task)typeof(NewOrderWorkspace).GetMethod("RecoverCommandAsync",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(component,[])!;
        Assert.Equal(command.CommandId,client.PendingCommandId);
        var current=(DemoOrderDraft)typeof(NewOrderWorkspace).GetField("_order",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(component)!;
        Assert.Equal(1,current.LegacyId);Assert.Single(transport.Bodies);
    }
    private static SalesOrderEntryClient Client(Storage storage,Transport transport,Guid? subject=null)
    {
        var actor=subject??Actor;var codec=new SalesCredentialProtector(new EphemeralDataProtectionProvider(),TimeProvider.System);
        var proof=codec.CreateSession(actor,"Employee",DateTimeOffset.UtcNow.AddMinutes(20));
        var auth=new Authentication(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,actor.ToString()),new Claim(SalesCredentialProtector.SessionClaim,proof)],"Cookies")));
        return new(new Factory(new HttpClient(transport){BaseAddress=new("https://test.invalid")}),new(auth,codec),storage);
    }
    private sealed class Authentication(ClaimsPrincipal principal):AuthenticationStateProvider {public override Task<AuthenticationState> GetAuthenticationStateAsync()=>Task.FromResult(new AuthenticationState(principal));}
    private sealed class Factory(HttpClient client):IHttpClientFactory{public HttpClient CreateClient(string name)=>client;}
    private sealed class Navigation:NavigationManager
    {
        public string? Target;
        public Navigation()=>Initialize("https://test.invalid/sales/","https://test.invalid/sales/");
        protected override void NavigateToCore(string uri,bool forceLoad)=>Target=ToAbsoluteUri(uri).AbsoluteUri;
        protected override void NavigateToCore(string uri,NavigationOptions options)=>Target=ToAbsoluteUri(uri).AbsoluteUri;
    }
    private sealed class Storage:IJSRuntime
    {
        public readonly Dictionary<string,string> Values=[];public bool FailRead,FailWrite;
        public ValueTask<T> InvokeAsync<T>(string identifier,object?[]? args)
        {
            if(identifier=="sessionStorage.getItem"){if(FailRead)throw new JSException("Read denied");return ValueTask.FromResult((T)(object?)Values.GetValueOrDefault((string)args![0]!)!);}
            if(identifier=="sessionStorage.setItem"){if(FailWrite)throw new JSException("Write denied");Values[(string)args![0]!]=(string)args[1]!;}
            if(identifier=="sessionStorage.removeItem")Values.Remove((string)args![0]!);
            return ValueTask.FromResult(default(T)!);
        }
        public ValueTask<T> InvokeAsync<T>(string identifier,CancellationToken ct,object?[]? args)=>InvokeAsync<T>(identifier,args);
    }
    private sealed class Transport:HttpMessageHandler
    {
        public bool FailFirst=true;public long? WrongResultOrder,OutcomeOrder;public List<string> Bodies=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if(request.Method==HttpMethod.Get)
            {
                if(OutcomeOrder is { } order)
                {
                    var id=Guid.Parse(request.RequestUri!.AbsolutePath.Split('/').Last());
                    return new(HttpStatusCode.OK){Content=JsonContent.Create(new CommandOutcome(id,"product.create.v1",order,null))};
                }
                return new(HttpStatusCode.NotFound){Content=JsonContent.Create(new{detail="No receipt"})};
            }
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            if(FailFirst && Bodies.Count==1)return new(HttpStatusCode.ServiceUnavailable){Content=JsonContent.Create(new{detail="Unknown original result"})};
            var segments=request.RequestUri!.AbsolutePath.Split('/');
            var orderId=WrongResultOrder ?? (segments.Length>5 ? long.Parse(segments[5]) : 42);
            return new(HttpStatusCode.OK){Content=JsonContent.Create(new{Order=new{Header=new{Id=orderId,Number="XXXX-1-26"},Products=Array.Empty<object>(),Duties=Array.Empty<object>(),Revision="r2",WritesAllowed=true,Warnings=Array.Empty<string>()},ProductId=(long?)4,Replayed=true})};
        }
    }
}
