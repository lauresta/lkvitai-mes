using LKvitai.MES.BuildingBlocks.ModuleStartup;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("frontline");
builder.Services.AddScaffoldApiCore();

var app = builder.Build();

app.UseScaffoldApiPipeline();

// TODO: tighten auth when roles are defined. Frontline is the safe field/branch
// surface (e.g. fabric availability); it stays anonymous until the role model
// is finalised, at which point this group will adopt the shared PortalAuth
// scheme and a read-only policy for operators in the field. Until then the
// endpoints are reachable without authentication because this app intentionally
// does not register AddAuthorization()/UseAuthentication/UseAuthorization — no
// AllowAnonymous() metadata is emitted here to avoid suggesting otherwise.
var frontline = app.MapGroup("/api/frontline");

frontline.MapGet("/ping", () => Results.Ok(new
{
    Module = "Frontline",
    Now = DateTimeOffset.UtcNow.ToString("O")
}));

await app.RunAsync();
