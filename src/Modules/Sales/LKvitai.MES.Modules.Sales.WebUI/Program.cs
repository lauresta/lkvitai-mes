using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("sales-webui");
builder.Services.AddScaffoldWebUiCore("SalesApi", "SalesApi:BaseUrl", "http://localhost:5021");
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<SalesApiAuthHandler>();
builder.Services
    .AddHttpClient("SalesApi")
    .AddHttpMessageHandler<SalesApiAuthHandler>();
builder.Services.AddScoped<SalesApiClient>();

var app = builder.Build();

app.UsePathBase(ScaffoldModuleStartupExtensions.ResolvePathBase(app.Configuration));
app.UsePortalSecureHosting(app.Environment);

app.UseSerilogRequestLogging();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok());
app.MapPortalLogout();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

await app.RunAsync();
