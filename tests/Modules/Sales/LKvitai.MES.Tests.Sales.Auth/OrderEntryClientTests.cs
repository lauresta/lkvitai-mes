using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class OrderEntryClientTests
{
    [Theory]
    [InlineData("VV-123","VV-123-26")]
    [InlineData("VV-123-A","VV-123-26A")]
    [InlineData("VV-123-26A","VV-123-26A")]
    [InlineData("RY-TEST-123","RY-TEST-123-26")]
    public void Number_preview_matches_a_complete_known_series_prefix(string input,string expected)
    {
        var catalog=new DemoCatalog{Series=[new DemoSeries{Id=1,Code="VV"},new DemoSeries{Id=2,Code="RY"},new DemoSeries{Id=3,Code="RY-TEST"}]};
        Assert.True(DemoOrderNumber.TryNormalize(input,2026,catalog,out var canonical,out _,out _));
        Assert.Equal(expected,canonical);
        Assert.False(DemoOrderNumber.TryNormalize("UNKNOWN-123",2026,catalog,out _,out _,out _));
    }

    [Fact]
    public async Task Unknown_save_outcome_replays_the_same_body_and_prevents_a_second_create()
    {
        var transport=new Transport();var js=new BrowserStorage();
        var client=Client(transport,js);
        var command=new CreateOrderCommand(Guid.NewGuid(),new OrderHeaderInput(123,"RY-TEST-123-26",new(2026,10,31),new(2026,11,14),1,2,null,null));
        var error=await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.CreateAsync(command));
        Assert.True(error.OutcomeUnknown);Assert.True(client.HasPendingCommand);
        Assert.Equal(command.CommandId,client.PendingCommandId);
        await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.CreateAsync(command with{CommandId=Guid.NewGuid()}));
        Assert.Single(transport.Bodies);
        var outcome=await client.RetryPendingAsync();
        Assert.Equal(42,outcome.Order.Header.Id);Assert.False(client.HasPendingCommand);
        Assert.Equal(transport.Bodies[0],transport.Bodies[1]);
        Assert.Contains(js.Calls,x=>x.Identifier=="sessionStorage.setItem"&&x.Args![0]!.ToString()!.StartsWith("sales-entry-outbox-v2:") && x.Args[1]!.ToString()!.Contains(command.CommandId.ToString()));
        Assert.Contains(js.Calls,x=>x.Identifier=="sessionStorage.removeItem");
    }
    [Fact]
    public async Task Confirmed_validation_failure_does_not_leave_an_unknown_command()
    {
        var transport=new Transport{FirstStatus=HttpStatusCode.UnprocessableEntity};
        var client=Client(transport,new BrowserStorage());
        var command=new CreateOrderCommand(Guid.NewGuid(),new OrderHeaderInput(1,"VV-1-26",new(2026,10,31),new(2026,11,14),1,2,null,null));
        var error=await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.CreateAsync(command));
        Assert.False(error.OutcomeUnknown);Assert.Equal(422,error.Status);Assert.False(client.HasPendingCommand);
    }
    [Fact]
    public async Task Payment_unknown_outcome_replays_the_same_financial_command_and_recovers_one_payment()
    {
        var transport=new PaymentTransport();
        var client=Client(transport,new BrowserStorage());
        var command=new PostOrderPaymentCommand(Guid.NewGuid(),"r1",new OrderPaymentInput(new(2026,10,31),1.25m,81423987,123,Note:"Test payment"));
        var failure=await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.PostPaymentAsync(42,command));
        Assert.True(failure.OutcomeUnknown);
        await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.PostPaymentAsync(42,command with{CommandId=Guid.NewGuid()}));
        var recovered=await client.RetryPendingAsync();
        Assert.True(recovered.Replayed);
        Assert.Equal(987,recovered.PaymentId);
        Assert.Equal(1.25m,recovered.Order.Header.Paid);
        Assert.Single(recovered.Order.Payments!);
        Assert.Equal(2,transport.Bodies.Count);
        Assert.Equal(transport.Bodies[0],transport.Bodies[1]);
        Assert.All(transport.Paths,path=>Assert.Equal("/api/sales/order-entry/orders/42/payments",path));
        Assert.False(client.HasPendingCommand);
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task Payment_unknown_then_auth_or_gate_failure_retains_original_command_until_successful_replay(int status)
    {
        var transport=new PaymentTransport((HttpStatusCode)status);
        var storage=new BrowserStorage();
        var client=Client(transport,storage);
        var command=new PostOrderPaymentCommand(Guid.NewGuid(),"r1",new OrderPaymentInput(new(2026,10,31),1.25m,81423987,123,Note:"Test payment"));
        await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.PostPaymentAsync(42,command));
        var auth=await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.RetryPendingAsync());
        Assert.Equal(status,auth.Status);
        Assert.True(auth.OutcomeUnknown);
        Assert.True(client.HasPendingCommand);
        Assert.Equal(command.CommandId,client.PendingCommandId);
        Assert.DoesNotContain(storage.Calls,call=>call.Identifier=="sessionStorage.removeItem");
        await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.PostPaymentAsync(42,command with{CommandId=Guid.NewGuid()}));
        Assert.Equal(2,transport.Bodies.Count);
        var recovered=await client.RetryPendingAsync();
        Assert.True(recovered.Replayed);
        Assert.Equal(987,recovered.PaymentId);
        Assert.Single(recovered.Order.Payments!);
        Assert.Equal(3,transport.Bodies.Count);
        Assert.All(transport.Bodies,body=>Assert.Equal(transport.Bodies[0],body));
        Assert.False(client.HasPendingCommand);
        Assert.Contains(storage.Calls,call=>call.Identifier=="sessionStorage.removeItem");
    }
    [Theory]
    [InlineData(401)]
    [InlineData(403)]
    public async Task First_definitive_auth_or_gate_failure_does_not_create_an_unknown_payment(int status)
    {
        var client=Client(new Transport{FirstStatus=(HttpStatusCode)status},new BrowserStorage());
        var command=new PostOrderPaymentCommand(Guid.NewGuid(),"r1",new OrderPaymentInput(new(2026,10,31),1.25m,81423987,123));
        var failure=await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.PostPaymentAsync(42,command));
        Assert.False(failure.OutcomeUnknown);
        Assert.False(client.HasPendingCommand);
    }
    [Fact]
    public async Task Confirmation_recovery_uses_the_same_selection_and_document_outcome()
    {
        var transport=new ConfirmationTransport();
        var client=Client(transport,new BrowserStorage());
        var command=new ConfirmOrderProductsCommand(Guid.NewGuid(),"r1",[3,7]);
        Assert.True((await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.ConfirmProductsAsync(42,command))).OutcomeUnknown);
        await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>client.ConfirmProductsAsync(42,command with{CommandId=Guid.NewGuid(),ProductIds=[3]}));
        var recovered=await client.RetryPendingAsync();
        Assert.True(recovered.Replayed);
        Assert.Equal(7654321,recovered.ProductionDocumentId);
        Assert.Null(recovered.PaymentId);
        Assert.Equal(2,transport.Bodies.Count);
        Assert.Equal(transport.Bodies[0],transport.Bodies[1]);
        Assert.False(client.HasPendingCommand);
    }
    private static SalesOrderEntryClient Client(HttpMessageHandler transport,BrowserStorage js)
    {
        var codec=new SalesCredentialProtector(new EphemeralDataProtectionProvider(),TimeProvider.System);
        var subject=Guid.NewGuid();var proof=codec.CreateSession(subject,"Employee",DateTimeOffset.UtcNow.AddHours(1));
        var state=new State(new ClaimsPrincipal(new ClaimsIdentity(new[]{new Claim(ClaimTypes.NameIdentifier,subject.ToString()),new Claim(SalesCredentialProtector.SessionClaim,proof)},"Cookies")));
        return new SalesOrderEntryClient(new Factory(new HttpClient(transport){BaseAddress=new Uri("https://test.invalid")}),new SalesSessionCredentials(state,codec),js);
    }
    private sealed class State(ClaimsPrincipal user):AuthenticationStateProvider
    {public override Task<AuthenticationState> GetAuthenticationStateAsync()=>Task.FromResult(new AuthenticationState(user));}
    private sealed class Factory(HttpClient client):IHttpClientFactory {public HttpClient CreateClient(string name)=>client;}
    private sealed class BrowserStorage:IJSRuntime
    {
        public List<(string Identifier,object?[]? Args)> Calls=[];
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args){Calls.Add((identifier,args));return ValueTask.FromResult(default(TValue)!);}
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken ct,object?[]? args)=>InvokeAsync<TValue>(identifier,args);
    }
    private sealed class Transport:HttpMessageHandler
    {
        public HttpStatusCode FirstStatus=HttpStatusCode.ServiceUnavailable;
        public List<string> Bodies=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            if(Bodies.Count==1)return new HttpResponseMessage(FirstStatus){Content=JsonContent.Create(new{code="DEPENDENCY_FAILURE",detail="Please retry this command."})};
            return new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new{Order=new{Header=new{Id=42,Number="RY-TEST-123-26"},Products=Array.Empty<object>(),Address=(object?)null,Duties=Array.Empty<object>(),Revision="v1",WritesAllowed=true,Warnings=Array.Empty<string>()},ProductId=(long?)null,Replayed=true})};
        }
    }
    private sealed class PaymentTransport(HttpStatusCode? retryFailure=null):HttpMessageHandler
    {
        public List<string> Bodies=[];
        public List<string> Paths=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            Paths.Add(request.RequestUri!.AbsolutePath);
            if(Bodies.Count==1)return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable){Content=JsonContent.Create(new{detail="Response lost after commit."})};
            if(Bodies.Count==2 && retryFailure is { } status)return new HttpResponseMessage(status){Content=JsonContent.Create(new{detail="Session expired or payment gate disabled."})};
            return new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new{
                Order=new{Header=new{Id=42,Number="XXXX-1-26",Paid=1.25m},Products=Array.Empty<object>(),Duties=Array.Empty<object>(),Revision="r2",WritesAllowed=true,Warnings=Array.Empty<string>(),
                    Payments=new[]{new{Id=987,Date="2026-10-31",Amount=1.25m,MethodId=81423987,EmployeeId=123,CurrencyId=1,CurrencyCode="EUR"}}},
                ProductId=(long?)null,PaymentId=987,Replayed=true})};
        }
    }
    private sealed class ConfirmationTransport:HttpMessageHandler
    {
        public List<string> Bodies=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            Assert.Equal("/api/sales/order-entry/orders/42/confirm",request.RequestUri!.AbsolutePath);
            Bodies.Add(await request.Content!.ReadAsStringAsync(ct));
            if(Bodies.Count==1)return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable){Content=JsonContent.Create(new{detail="Production response lost."})};
            return new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new{
                Order=new{Header=new{Id=42,Number="XXXX-1-26"},Products=Array.Empty<object>(),Duties=Array.Empty<object>(),Revision="r2",WritesAllowed=true,Warnings=Array.Empty<string>()},
                ProductId=(long?)null,ProductionDocumentId=7654321,Replayed=true})};
        }
    }
}
