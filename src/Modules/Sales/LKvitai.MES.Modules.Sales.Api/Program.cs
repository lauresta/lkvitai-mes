using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("sales");
builder.Services.AddScaffoldApiCore();

// Sales replicates Warehouse's "internal user mechanism" by leaning on the shared
// Portal cookie (PortalAuth). Same DataProtection keys, same cookie name, so a user
// signed in via Portal is authenticated here too. Once real role-based authorization
// is added to Sales, this will be tightened to match Warehouse's WarehouseHeader
// scheme (see src/Modules/Warehouse/.../Api/Security/WarehouseAuthenticationHandler.cs).
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAuthorization();

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

await app.RunAsync();
