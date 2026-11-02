using LKvitai.MES.BuildingBlocks.WebUI.Localization;
using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.BuildingBlocks.WebUI.Services;
using LKvitai.MES.Modules.Shopfloor.WebUI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("shopfloor-webui");
builder.Services.AddScaffoldWebUiCore("ShopfloorApi", "ShopfloorApi:BaseUrl", "http://localhost:5041");
builder.Services.AddCikadaLocalization(builder.Configuration);

builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddBuildVersion();
builder.Services.AddScoped<ShopfloorDatabaseStatusService>();

builder.Services.AddTransient<ShopfloorApiAuthHandler>();
builder.Services
    .AddHttpClient("ShopfloorApi")
    .AddHttpMessageHandler<ShopfloorApiAuthHandler>();
builder.Services.AddScoped<ShopfloorApiClient>();

var app = builder.Build();

app.UsePathBase(ScaffoldModuleStartupExtensions.ResolvePathBase(app.Configuration));
// Shared EN/LT UI culture from the cross-module culture cookie (docs/i18n/README.md).
app.UseCikadaRequestLocalization();
app.UsePortalSecureHosting(app.Environment);

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok());
app.MapPortalLogout();
app.MapCikadaCultureEndpoint();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();
