using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.Modules.Frontline.WebUI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("frontline-webui");
builder.Services.AddScaffoldWebUiCore("FrontlineApi", "FrontlineApi:BaseUrl", "http://localhost:5031");

// F-1 wires the named "FrontlineApi" HttpClient (registered by
// AddScaffoldWebUiCore above) to the FrontlineApiClient wrapper used by the
// fabric lookup + low-stock pages. No DelegatingHandler is registered today
// because the Frontline API is anonymous in F-1; once F-2.4 introduces the
// shared auth scheme, the named client will pick up an AddHttpMessageHandler
// here and FrontlineApiClient itself will not need to change.
builder.Services.AddScoped<FrontlineApiClient>();

var app = builder.Build();

app.UseScaffoldWebUiPipeline();

await app.RunAsync();
