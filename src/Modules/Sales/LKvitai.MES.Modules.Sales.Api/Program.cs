using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.Api.Composition;
using LKvitai.MES.Modules.Sales.Api.Endpoints;
using LKvitai.MES.Modules.Sales.Api.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("sales");
builder.Services.AddScaffoldApiCore();

// S-2: bind the orders read-side to the SQL Server adapter
// (SqlOrdersQueryService over the legacy weblb_* stored procedures). Requires
// ConnectionStrings:LKvitaiDb in every environment — Sales.Api now refuses to
// start without it. The in-memory StubOrdersQueryService is only used when
// Sales:OrdersDataSource is explicitly set to "Stub" (test / dev opt-in).
builder.Services.AddSalesOrdersDataSource(builder.Configuration, builder.Environment);

// Sales replicates Warehouse's "internal user mechanism" by leaning on the shared
// Portal cookie (PortalAuth). Same DataProtection keys, same cookie name, so a user
// signed in via Portal is authenticated here too. Once real role-based authorization
// is added to Sales, this will be tightened to match Warehouse's WarehouseHeader
// scheme (see src/Modules/Warehouse/.../Api/Security/WarehouseAuthenticationHandler.cs).
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services
    .AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, PortalStructuredBearerAuthenticationHandler>(
        PortalStructuredBearerAuthenticationDefaults.Scheme,
        _ => { })
    // Dev-only synthetic identity. Returns NoResult unless the host is in
    // Development/Test AND Sales:DevAuthEnabled=true, so it cannot weaken
    // production auth. See SalesDevAuthenticationHandler for the rationale
    // for using a scheme instead of plain middleware.
    .AddScheme<AuthenticationSchemeOptions, SalesDevAuthenticationHandler>(
        SalesDevAuthDefaults.Scheme,
        _ => { });

var defaultAuthSchemes = new List<string>
{
    CookieAuthenticationDefaults.AuthenticationScheme,
    PortalStructuredBearerAuthenticationDefaults.Scheme,
};
if (builder.Environment.IsDevelopment()
    || string.Equals(builder.Environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
{
    defaultAuthSchemes.Add(SalesDevAuthDefaults.Scheme);
}

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(defaultAuthSchemes.ToArray())
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseScaffoldApiPipeline();
app.UseAuthentication();
app.UseAuthorization();

var sales = app.MapGroup("/api/sales").RequireAuthorization();

sales.MapGet("/ping", () => Results.Ok(new
{
    Module = "Sales",
    Now = DateTimeOffset.UtcNow.ToString("O")
}));

sales.MapOrdersEndpoints();

await app.RunAsync();
