using LKvitai.MES.BuildingBlocks.ModuleStartup;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("scanning-webui");
builder.Services.AddScaffoldWebUiCore("ScanningApi", "ScanningApi:BaseUrl", "http://localhost:5041");
// Needed by the shared ClarityAnalytics component (_Layout.cshtml) to read
// the current request's User - Scanning has no PortalAuth wired up so this
// resolves to an unauthenticated principal, same as every other page here.
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseScaffoldWebUiPipeline();

await app.RunAsync();
