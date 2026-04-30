using LKvitai.MES.BuildingBlocks.ModuleStartup;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("frontline-webui");
builder.Services.AddScaffoldWebUiCore("FrontlineApi", "FrontlineApi:BaseUrl", "http://localhost:5031");

var app = builder.Build();

app.UseScaffoldWebUiPipeline();

await app.RunAsync();
