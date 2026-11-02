using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using LKvitai.MES.Modules.Sales.Api.Endpoints;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class OrderEntryWriteBoundaryTests
{
    [Fact]
    public async Task Write_routes_require_trusted_actor_explicit_enablement_and_reject_overposting()
    {
        var builder=WebApplication.CreateBuilder();builder.WebHost.UseTestServer();
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddSalesProtectedAuthentication();
        builder.Services.AddSingleton<ISalesOrderEntryService>(new SqlSalesOrderEntryService(
            new SalesSqlOptions{ConnectionString="Server=must-not-connect;Database=none"},new SalesOrderEntryOptions(),new RejectingLegacySqlConnectionFactory()));
        await using var app=builder.Build();app.UseAuthentication();app.UseAuthorization();
        app.MapGroup("/api/sales").RequireAuthorization(SalesCredentialProtector.Policy).MapOrderEntryEndpoints();
        await app.StartAsync();var client=app.GetTestClient();
        var command=new CreateOrderCommand(Guid.NewGuid(),new OrderHeaderInput(2136330188,"RY-TEST-123",new DateOnly(2026,10,31),new DateOnly(2026,11,14),227095126,-637977188,null,null));
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/sales/order-entry/orders",command)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.GetAsync("/api/sales/order-entry/orders/1/payment-references")).StatusCode);
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer","forged|admin");
        Assert.Equal(HttpStatusCode.Unauthorized,(await client.PostAsJsonAsync("/api/sales/order-entry/orders",command)).StatusCode);
        var codec=app.Services.GetRequiredService<SalesCredentialProtector>();
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",codec.CreateApi(codec.CreateSession(Guid.NewGuid(),"Employee",DateTimeOffset.UtcNow.AddHours(1))));
        var denied=await client.PostAsJsonAsync("/api/sales/order-entry/orders",command);
        Assert.Equal(HttpStatusCode.Forbidden,denied.StatusCode);Assert.True(denied.Headers.CacheControl?.NoStore);
        Assert.Contains("WRITES_DISABLED",await denied.Content.ReadAsStringAsync());
        var overposted=new {command.CommandId,command.Header,ActorId=Guid.NewGuid()};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/sales/order-entry/orders",overposted)).StatusCode);
        var configure=new ConfigureProductCommand(Guid.NewGuid(),"revision",new ProductConfigurationInput(-1025240863,800,1500,1,2328,"D",20.8526m,true,null,new string?[12]));
        Assert.Equal(HttpStatusCode.Forbidden,(await client.PutAsJsonAsync("/api/sales/order-entry/orders/1/products/2",configure)).StatusCode);
        var payment=new PostOrderPaymentCommand(Guid.NewGuid(),"revision",new OrderPaymentInput(new DateOnly(2026,10,31),0.01m,81423987,77249366));
        var paymentDenied=await client.PostAsJsonAsync("/api/sales/order-entry/orders/1/payments",payment);
        Assert.Equal(HttpStatusCode.Forbidden,paymentDenied.StatusCode);
        Assert.Contains("PAYMENTS_DISABLED",await paymentDenied.Content.ReadAsStringAsync());
        // Receiver, currency and actor are server-scoped. A currency field may
        // not be smuggled into the nested payment body to change that contract.
        var paymentOverposted=new{payment.CommandId,payment.ExpectedRevision,Payment=new{payment.Payment.Date,payment.Payment.Amount,payment.Payment.MethodId,payment.Payment.EmployeeId,CurrencyId=1}};
        Assert.Equal(HttpStatusCode.BadRequest,(await client.PostAsJsonAsync("/api/sales/order-entry/orders/1/payments",paymentOverposted)).StatusCode);
    }
}
