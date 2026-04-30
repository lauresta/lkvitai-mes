using LKvitai.MES.BuildingBlocks.ModuleStartup;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("scanning-webui");
builder.Services.AddScaffoldWebUiCore("ScanningApi", "ScanningApi:BaseUrl", "http://localhost:5041");

var app = builder.Build();

app.UseScaffoldWebUiPipeline();

await app.RunAsync();
