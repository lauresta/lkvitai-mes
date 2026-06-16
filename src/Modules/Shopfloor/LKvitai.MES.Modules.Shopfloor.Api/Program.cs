using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Shopfloor.Api.Endpoints;
using LKvitai.MES.Modules.Shopfloor.Api.Middleware;
using LKvitai.MES.Modules.Shopfloor.Api.Persistence;
using LKvitai.MES.Modules.Shopfloor.Api.Security;
using LKvitai.MES.Modules.Shopfloor.Application;
using LKvitai.MES.Modules.Shopfloor.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("shopfloor-api");
builder.Services.AddScaffoldApiCore();

builder.Services.AddShopfloorApplication();
builder.Services.AddShopfloorInfrastructure(builder.Configuration);

// Same Portal auth pattern used by Sales: shared Portal cookie + structured
// bearer, plus a dev-only synthetic identity gated behind Shopfloor:DevAuthEnabled.
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services
    .AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, PortalStructuredBearerAuthenticationHandler>(
        PortalStructuredBearerAuthenticationDefaults.Scheme,
        _ => { })
    .AddScheme<AuthenticationSchemeOptions, ShopfloorDevAuthenticationHandler>(
        ShopfloorDevAuthDefaults.Scheme,
        _ => { });

var defaultAuthSchemes = new List<string>
{
    CookieAuthenticationDefaults.AuthenticationScheme,
    PortalStructuredBearerAuthenticationDefaults.Scheme,
};
if (builder.Environment.IsDevelopment()
    || string.Equals(builder.Environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
{
    defaultAuthSchemes.Add(ShopfloorDevAuthDefaults.Scheme);
}

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(defaultAuthSchemes.ToArray())
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

await ShopfloorDbMigrator.MigrateAsync(app.Services).ConfigureAwait(false);

app.UseScaffoldApiPipeline();
app.UseMiddleware<ShopfloorExceptionMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

var shopfloor = app.MapGroup("/api/shopfloor").RequireAuthorization();

shopfloor.MapGet("/ping", () => Results.Ok(new
{
    Module = "Shopfloor",
    Now = DateTimeOffset.UtcNow.ToString("O"),
}));

shopfloor.MapWorkCentersEndpoints();
shopfloor.MapWorkStationsEndpoints();
shopfloor.MapWorkflowsEndpoints();
shopfloor.MapLegacyProductTypesEndpoints();
shopfloor.MapProductTypeMappingsEndpoints();

await app.RunAsync();
