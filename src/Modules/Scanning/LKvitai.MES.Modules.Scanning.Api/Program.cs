using LKvitai.MES.BuildingBlocks.ModuleStartup;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("scanning");
builder.Services.AddScaffoldApiCore();

var app = builder.Build();

app.UseScaffoldApiPipeline();

// TODO: tighten auth when roles are defined. Scanning is the cross-cutting
// mobile barcode lookup surface; it stays anonymous until the role model is
// finalised, at which point this group will adopt the shared PortalAuth scheme
// and enforce a minimal "scanner" policy for operator identification. Until
// then the endpoints are reachable without authentication because this app
// intentionally does not register AddAuthorization()/UseAuthentication/
// UseAuthorization — no AllowAnonymous() metadata is emitted here to avoid
// suggesting otherwise.
var scanning = app.MapGroup("/api/scan");

scanning.MapGet("/ping", () => Results.Ok(new
{
    Module = "Scanning",
    Now = DateTimeOffset.UtcNow.ToString("O")
}));

await app.RunAsync();
