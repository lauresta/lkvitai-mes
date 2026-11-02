using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.Api.Composition;
using LKvitai.MES.Modules.Sales.Api.Endpoints;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.Modules.Sales.Application.Ports;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql.Security;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("sales");
builder.Services.AddScaffoldApiCore();

// S-2: bind the orders read-side to the SQL Server adapter
// (SqlOrdersQueryService over the legacy weblb_* stored procedures). Requires
// ConnectionStrings:LKvitaiDb in every environment — Sales.Api now refuses to
// start without it. The in-memory StubOrdersQueryService is only used when
// Sales:OrdersDataSource is explicitly set to "Stub" (test / dev opt-in).
builder.Services.AddSalesOrdersDataSource(builder.Configuration, builder.Environment);
// Explicit Stub hosts have no live SQL options. Keep their sample-order path
// usable without registering/misrepresenting a live reference service.
var liveReferencesConfigured = builder.Services.Any(x => x.ServiceType == typeof(SalesSqlOptions));
if (liveReferencesConfigured)
{
    builder.Services.AddHttpContextAccessor();
    builder.Services.AddSingleton<CikadaUserBridgeProof>();
    builder.Services.AddScoped<ILegacySqlIdentityResolver,CikadaLegacySqlIdentityResolver>();
    builder.Services.AddScoped<ILegacySqlConnectionFactory,LegacySqlConnectionFactory>();
    builder.Services.AddHttpClient("CikadaUsers", client=>
    {
        client.BaseAddress=new Uri(builder.Configuration["CikadaUsers:BaseUrl"] ?? "http://localhost:5001");
        client.Timeout=TimeSpan.FromSeconds(10);
    }).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
    builder.Services.AddScoped<ISalesOrderEntryReferences, SqlSalesOrderEntryReferences>();
    builder.Services.AddScoped<ISalesReports, SqlSalesReports>();
    builder.Services.AddScoped<ISalesUnitPriceLookup, SqlSalesUnitPriceLookup>();
    builder.Services.AddScoped<ILiveOrderEntryCatalog, SqlLiveOrderEntryCatalog>();
    builder.Services.AddScoped<IOrderEntryAddressLookup, SqlOrderEntryAddressLookup>();
    builder.Services.AddScoped<ILegacyProductPreview, SqlLegacyProductPreview>();
    builder.Services.AddSingleton(builder.Configuration.GetSection("Sales:OrderEntry").Get<SalesOrderEntryOptions>() ?? new SalesOrderEntryOptions());
    builder.Services.AddScoped<ISalesOrderEntryService, SqlSalesOrderEntryService>();
    builder.Services.AddScoped<ISalesOrderFinancialCommands>(sp => (ISalesOrderFinancialCommands)sp.GetRequiredService<ISalesOrderEntryService>());
}

// Portal key-ring configuration is reused; real Sales endpoints accept only
// the purpose-bound Sales credential, not Portal cookies or raw structured tokens.
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddSalesProtectedAuthentication(builder.Configuration);

var app = builder.Build();

app.UseScaffoldApiPipeline();
app.UseAuthentication();
app.UseAuthorization();
// Mapping and horizontal-access failures apply to every Sales route, including
// list/catalog/preview endpoints outside the command group.
app.Use(async(context,next)=>
{
    try { await next(context); }
    catch(Microsoft.Data.SqlClient.SqlException ex)when(ex.Number is 229 or 230 && !context.Response.HasStarted)
    {
        context.Response.StatusCode=403;
        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        { Status=403,Title="Your legacy SQL account does not permit this operation.",Extensions={ ["code"]="LEGACY_SQL_PERMISSION_DENIED" } });
    }
    catch(LKvitai.MES.Modules.Sales.Contracts.OrderEntry.OrderEntryException ex) when(!context.Response.HasStarted)
    {
        context.Response.StatusCode=ex.Status;
        await context.Response.WriteAsJsonAsync(new Microsoft.AspNetCore.Mvc.ProblemDetails
        { Status=ex.Status,Title=ex.Message,Extensions={ ["code"]=ex.Code } });
    }
});

var sales = app.MapGroup("/api/sales").RequireAuthorization(LKvitai.MES.BuildingBlocks.PortalAuth.SalesCredentialProtector.Policy);

sales.MapGet("/ping", () => Results.Ok(new
{
    Module = "Sales",
    Now = DateTimeOffset.UtcNow.ToString("O")
}));

sales.MapOrdersEndpoints();
if (liveReferencesConfigured)
{
    sales.MapOrderEntryReferenceEndpoints();
    sales.MapOrderEntryPricingEndpoints();
    sales.MapOrderEntryCatalogEndpoints();
    sales.MapOrderEntryPreviewEndpoints();
    sales.MapOrderEntryEndpoints();
    sales.MapReportsEndpoints();
}

await app.RunAsync();
