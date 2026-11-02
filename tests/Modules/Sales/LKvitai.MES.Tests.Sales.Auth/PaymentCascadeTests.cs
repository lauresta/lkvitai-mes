using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Security.Claims;
using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.Modules.Sales.Infrastructure.Security;
using LKvitai.MES.Modules.Sales.WebUI.Components;
using LKvitai.MES.Modules.Sales.WebUI.Models;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.JSInterop;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class PaymentCascadeTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private static readonly DateOnly BusinessDate=new(2026,11,1);
    private static T Get<T>(NewOrderWorkspace component,string name)=>(T)typeof(NewOrderWorkspace).GetField(name,Private)!.GetValue(component)!;
    private static void Set(NewOrderWorkspace component,string name,object? value)=>typeof(NewOrderWorkspace).GetField(name,Private)!.SetValue(component,value);
    private static bool Flag(NewOrderWorkspace component,string name)=>(bool)typeof(NewOrderWorkspace).GetProperty(name,Private)!.GetValue(component)!;
    private static object? Invoke(NewOrderWorkspace component,string name,object? value=null)=>typeof(NewOrderWorkspace).GetMethod(name,Private)!.Invoke(component,value is null?[]:[new ChangeEventArgs{Value=value}]);
    private static Task Change(NewOrderWorkspace component,string name,string value)=>(Task)Invoke(component,name,value)!;

    [Fact]
    public async Task Method_change_clears_optional_and_downstream_fields_then_bank_selection_uses_source_default()
    {
        var (component,_) = Create();
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.CashMethodId.ToString());
        Assert.True(Flag(component,"PaymentDownstreamEnabled"));
        Invoke(component,"ChangePaymentEmployee","30");Invoke(component,"ChangePaymentAmount","3.25");Set(component,"_paymentNote","memo");
        Set(component,"_paymentReference","RRL0000001");Set(component,"_paymentRegisterId",1);
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.BankMethodId.ToString());
        Assert.False(Flag(component,"PaymentDownstreamEnabled"));
        Assert.Null(Get<int?>(component,"_paymentEmployeeId"));
        Assert.Null(Get<int?>(component,"_paymentRegisterId"));
        Assert.Equal("",Get<string>(component,"_paymentReference"));
        Assert.Equal("",Get<string>(component,"_paymentAmountText"));
        Assert.Equal("",Get<string>(component,"_paymentNote"));
        await Change(component,"ChangePaymentBank","101");
        Assert.True(Flag(component,"PaymentDownstreamEnabled"));
        // The UI follows the reference response, rather than embedding the native ID.
        Assert.Equal(91,Get<int?>(component,"_paymentEmployeeId"));
        Invoke(component,"ChangePaymentAmount","3.25");Set(component,"_paymentNote","bank memo");Invoke(component,"ChangePaymentEmployee","30");
        await Change(component,"ChangePaymentBank","103");
        Assert.Equal(91,Get<int?>(component,"_paymentEmployeeId"));
        Assert.Equal(3.25m,Get<decimal>(component,"_paymentAmount"));
        Assert.Equal("bank memo",Get<string>(component,"_paymentNote"));
        Assert.Equal("2026-11-01",Get<string>(component,"_paymentDate"));
    }

    [Fact]
    public async Task Register_default_is_selected_before_employee_and_employee_override_keeps_register()
    {
        var (component,_) = Create();
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.RegisterMethodId.ToString());
        Assert.False(Flag(component,"PaymentDownstreamEnabled"));
        await Change(component,"ChangePaymentRegister","1");
        Assert.True(Flag(component,"PaymentDownstreamEnabled"));
        Assert.Equal(20,Get<int?>(component,"_paymentEmployeeId"));
        Assert.True(Get<OrderPaymentReferences>(component,"_paymentReferences").Employees.Single(x=>x.Id==20).IsHidden);
        Invoke(component,"ChangePaymentAmount","0.50");Set(component,"_paymentNote","register memo");
        Invoke(component,"ChangePaymentEmployee","30");
        Assert.Equal(1,Get<int?>(component,"_paymentRegisterId"));
        Assert.Equal(30,Get<int?>(component,"_paymentEmployeeId"));
        await Change(component,"ChangePaymentRegister","2");
        Assert.Equal(21,Get<int?>(component,"_paymentEmployeeId"));
        Assert.Equal(0.50m,Get<decimal>(component,"_paymentAmount"));
        Assert.Equal("register memo",Get<string>(component,"_paymentNote"));
        await Change(component,"ChangePaymentRegister","3");
        Assert.True(Flag(component,"PaymentDownstreamEnabled"));
        Assert.Null(Get<int?>(component,"_paymentEmployeeId"));
        Invoke(component,"ChangePaymentEmployee","30");
        Assert.Equal(3,Get<int?>(component,"_paymentRegisterId"));
        Assert.Equal(30,Get<int?>(component,"_paymentEmployeeId"));
    }

    [Fact]
    public async Task Valid_ppk_enables_receiver_invalid_receipt_clears_downstream_without_changing_business_date()
    {
        var (component,_) = Create();
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.PpkMethodId.ToString());
        Assert.False(Flag(component,"PaymentDownstreamEnabled"));
        Invoke(component,"ChangePaymentPpk","RRL0000001");
        Assert.True(Flag(component,"PaymentDownstreamEnabled"));
        Invoke(component,"ChangePaymentEmployee","30");Invoke(component,"ChangePaymentAmount","1.20");Set(component,"_paymentNote","PPK memo");
        Invoke(component,"ChangePaymentPpk","RRL0000002");
        Assert.Equal(1.20m,Get<decimal>(component,"_paymentAmount"));
        Assert.Equal(30,Get<int?>(component,"_paymentEmployeeId"));
        Invoke(component,"ChangePaymentPpk","short");
        Assert.False(Flag(component,"PaymentDownstreamEnabled"));
        Assert.Null(Get<int?>(component,"_paymentEmployeeId"));
        Assert.Equal("",Get<string>(component,"_paymentAmountText"));
        Assert.Equal("",Get<string>(component,"_paymentNote"));
        Invoke(component,"ChangePaymentPpk","XXXXXXXXXX");
        Assert.False(Flag(component,"PaymentDownstreamEnabled"));
        Assert.Equal("2026-11-01",Get<string>(component,"_paymentDate"));
    }

    [Fact]
    public async Task Late_register_context_cannot_restore_a_default_after_switching_method()
    {
        var (component,transport) = Create();
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.RegisterMethodId.ToString());
        transport.DelayRegister=true;
        var pending=Change(component,"ChangePaymentRegister","1");
        await transport.RegisterEntered.Task;
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.CashMethodId.ToString());
        transport.ReleaseRegister.SetResult();await pending;
        Assert.Equal(LegacyPaymentRules.CashMethodId,Get<int?>(component,"_paymentMethodId"));
        Assert.Null(Get<int?>(component,"_paymentRegisterId"));
        Assert.Null(Get<int?>(component,"_paymentEmployeeId"));
        Assert.True(Flag(component,"PaymentDownstreamEnabled"));
    }

    [Fact]
    public async Task Unknown_pending_payment_freezes_parent_choices_and_does_not_start_another_lookup()
    {
        var (component,transport) = Create();
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.CashMethodId.ToString());
        Invoke(component,"ChangePaymentEmployee","30");Invoke(component,"ChangePaymentAmount","1.25");
        var command=new PostOrderPaymentCommand(Guid.NewGuid(),"r1",new(BusinessDate,1.25m,LegacyPaymentRules.CashMethodId,30));
        await Assert.ThrowsAsync<SalesOrderEntryFailure>(()=>component.Entry.PostPaymentAsync(1,command));
        var lookups=transport.GetCount;
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.BankMethodId.ToString());
        Invoke(component,"ChangePaymentPpk","RRL0000001");Invoke(component,"ChangePaymentEmployee","91");
        Assert.Equal(LegacyPaymentRules.CashMethodId,Get<int?>(component,"_paymentMethodId"));
        Assert.Equal(30,Get<int?>(component,"_paymentEmployeeId"));
        Assert.Equal(1.25m,Get<decimal>(component,"_paymentAmount"));
        Assert.Equal(lookups,transport.GetCount);
        Assert.Equal(command.CommandId,component.Entry.PendingCommandId);
    }

    [Theory]
    [InlineData("1,23")]
    [InlineData("0.001")]
    [InlineData("0.0001")]
    public async Task Ambiguous_separator_or_subcent_payment_never_reaches_post(string raw)
    {
        var (component,transport) = Create();
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.CashMethodId.ToString());
        Invoke(component,"ChangePaymentEmployee","30");Invoke(component,"ChangePaymentAmount",raw);
        await (Task)Invoke(component,"PostLivePaymentAsync")!;
        Assert.Equal(0,transport.PostCount);
        Assert.NotEmpty(Get<string>(component,"_paymentError"));
        if(raw.Contains(','))Assert.Equal(0,Get<decimal>(component,"_paymentAmount"));
    }

    [Fact]
    public async Task Fractional_bank_balance_is_not_rounded_to_a_payable_cent()
    {
        var (component,_) = Create();
        await Change(component,"ChangePaymentMethod",LegacyPaymentRules.BankMethodId.ToString());
        await Change(component,"ChangePaymentBank","102");
        Assert.False(Flag(component,"PaymentDownstreamEnabled"));
        var formatted=(string)typeof(NewOrderWorkspace).GetMethod("BankAmount",BindingFlags.Static|BindingFlags.NonPublic)!.Invoke(null,[0.0001m])!;
        Assert.Equal("0,0001",formatted);
    }

    private static (NewOrderWorkspace,CascadeTransport) Create()
    {
        var codec=new SalesCredentialProtector(new EphemeralDataProtectionProvider(),TimeProvider.System);
        var subject=Guid.NewGuid();var proof=codec.CreateSession(subject,"Fixture",DateTimeOffset.UtcNow.AddMinutes(20));
        var authentication=new FixtureAuthentication(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier,subject.ToString()),new Claim(SalesCredentialProtector.SessionClaim,proof)],"Cookies")));
        var transport=new CascadeTransport();
        var component=new NewOrderWorkspace{Entry=new SalesOrderEntryClient(new ClientFactory(new HttpClient(transport){BaseAddress=new Uri("https://test.invalid")}),new SalesSessionCredentials(authentication,codec),new NoJs())};
        Set(component,"_order",new DemoOrderDraft{LegacyId=1,Revision="r1",PaymentsEnabled=true,WritesAllowed=true,AllowedPaymentMethods=Methods.Select(x=>x.Id).ToArray()});
        Set(component,"_headerSaved",System.Text.Json.JsonSerializer.Serialize(typeof(NewOrderWorkspace).GetMethod("HeaderInput",Private)!.Invoke(component,[])));
        Set(component,"_addressSaved","null");
        Set(component,"_paymentReferences",References(null,null,null));
        Set(component,"_paymentDate","2026-11-01");
        return(component,transport);
    }
    private static readonly OrderPaymentMethodReference[] Methods=[
        new(LegacyPaymentRules.CashMethodId,"Cash"),new(LegacyPaymentRules.PpkMethodId,"PPK",RequiresPpkReceipt:true),
        new(LegacyPaymentRules.BankMethodId,"Bank",RequiresBankTransfer:true,DefaultEmployeeId:91),new(LegacyPaymentRules.RegisterMethodId,"EKA",RequiresRegister:true)];
    private static readonly OrderPaymentRegisterReference[] Registers=[new(1,"EKA one",20,2,"Hidden one",true),new(2,"EKA two",21,3,"Hidden two",true),new(3,"Missing default",null,4)];
    private static readonly OrderPaymentBankTransferReference[] Banks=[new(101,BusinessDate,10.1234m,"Actual payer","Purpose",IsAvailable:true,DefaultEmployeeId:91),new(102,BusinessDate,0.0001m,"Fractional","Purpose",IsAvailable:false,UnavailableReason:"Below minimum stored payment",DefaultEmployeeId:91),new(103,BusinessDate,4.50m,"Another payer","Purpose",DefaultEmployeeId:91)];
    private static OrderPaymentReferences References(int? method,int? bank,int? register)
    {
        var selectedBank=Banks.FirstOrDefault(x=>x.Id==bank);
        var selectedRegister=Registers.FirstOrDefault(x=>x.Id==register);
        var employees=new List<OrderPaymentEmployeeReference>{new(30,"Visible"),new(91,"Bank default")};
        if(selectedRegister?.DefaultEmployeeId is { } id)employees.Add(new(id,selectedRegister.DefaultEmployeeName!,IsHidden:true));
        var prerequisite=method==LegacyPaymentRules.BankMethodId ? selectedBank is null?"Choose transfer":selectedBank.IsAvailable?null:selectedBank.UnavailableReason : method==LegacyPaymentRules.RegisterMethodId && selectedRegister is null?"Choose register":null;
        return new(true,null,BusinessDate,"r1",new(2,"EUR",1),method==LegacyPaymentRules.BankMethodId && selectedBank?.IsAvailable==true?91:selectedRegister?.DefaultEmployeeId,null,Methods,employees,Registers,[],new(method,bank,register),selectedBank,selectedRegister,prerequisite,MaximumAmount:1000m);
    }
    private sealed class CascadeTransport:HttpMessageHandler
    {
        public int GetCount,PostCount;
        public bool DelayRegister;
        public TaskCompletionSource RegisterEntered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseRegister=new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            if(request.Method==HttpMethod.Post){PostCount++;return new(HttpStatusCode.ServiceUnavailable){Content=JsonContent.Create(new{detail="Unknown test outcome"})};}
            GetCount++;
            var query=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(request.RequestUri!.Query);
            int? Id(string key)=>int.TryParse(query.GetValueOrDefault(key).ToString(),out var value)?value:null;
            if(request.RequestUri.AbsolutePath.EndsWith("payment-bank-transfers"))return new(HttpStatusCode.OK){Content=JsonContent.Create(new OrderPaymentBankTransferPage(Banks,Banks.Length,1,25,Banks.FirstOrDefault(x=>x.Id==Id("selectedId"))))};
            var register=Id("registerId");
            if(DelayRegister && register==1){RegisterEntered.TrySetResult();await ReleaseRegister.Task;}
            return new(HttpStatusCode.OK){Content=JsonContent.Create(References(Id("methodId"),Id("bankTransferId"),register))};
        }
    }
    private sealed class FixtureAuthentication(ClaimsPrincipal principal):AuthenticationStateProvider {public override Task<AuthenticationState> GetAuthenticationStateAsync()=>Task.FromResult(new AuthenticationState(principal));}
    private sealed class ClientFactory(HttpClient client):IHttpClientFactory {public HttpClient CreateClient(string name)=>client;}
    private sealed class NoJs:IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,object?[]? args)=>ValueTask.FromResult(default(TValue)!);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,CancellationToken ct,object?[]? args)=>InvokeAsync<TValue>(identifier,args);
    }
}
