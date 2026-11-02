using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.WebUI.Localization;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("scanning-webui");
builder.Services.AddScaffoldWebUiCore("ScanningApi", "ScanningApi:BaseUrl", "http://localhost:5041");
// Needed by the shared ClarityAnalytics component (_Layout.cshtml) to read
// the current request's User - Scanning has no PortalAuth wired up so this
// resolves to an unauthenticated principal, same as every other page here.
builder.Services.AddHttpContextAccessor();
builder.Services.AddCikadaLocalization(builder.Configuration);

var app = builder.Build();

// Shared EN/LT UI culture cookie (docs/i18n/README.md); runs before the scaffold pipeline's routing.
app.UseCikadaRequestLocalization();
app.UseScaffoldWebUiPipeline();
app.MapCikadaCultureEndpoint();

await app.RunAsync();
