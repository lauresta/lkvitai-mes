using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.BuildingBlocks.WebUI.Services;
using LKvitai.MES.Modules.Frontline.WebUI.Services;
using Serilog;

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

// Server-rendered QR code generator for the FabricLookup "Open on phone"
// sidebar (and, ahead of #96, the wall-printable per-fabric QR). Singleton
// because QRCoder's QRCodeGenerator wraps a thread-safe internal cache
// and a fresh instance per circuit would just thrash the GC for no win.
builder.Services.AddSingleton<FabricLookupQrCodeBuilder>();

// #92 — Decode the shared portal cookie so PortalModuleShell can surface
// the real admin name in the user-block. Frontline does NOT use this scheme
// to enforce any route (no [Authorize] / RequireAuthorization here on
// purpose — the fabric lookup stays anonymous for sales-floor handsets that
// scan the QR without ever signing in). What this gives us is:
//   * AuthenticationStateProvider populates with the cookie's claims when a
//     visitor is signed into Portal, so MainLayout's PortalModuleShell.User
//     becomes the real ClaimsPrincipal and DisplayName/Initials resolve
//     properly.
//   * Anonymous visitors still get an unauthenticated principal — the shell
//     falls back to "Signed in" / "U" and every page keeps rendering.
// Sharing the cookie across modules requires the shared DataProtection key
// ring (PortalAuth__DataProtectionKeysPath in docker-compose.test.yml +
// AUTH_KEYS_DIR bind mount on the host).
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddHttpContextAccessor();
// Surfaces APP_VERSION / GIT_SHA / BUILD_DATE env vars (injected by
// build-and-push.yml → Frontline.WebUI Dockerfile ARG → ENV) into MainLayout
// so the shared topbar's "VER" badge shows the real release tag instead of
// an em-dash. Same registration Portal/Sales call.
builder.Services.AddBuildVersion();

var app = builder.Build();

// UseScaffoldWebUiPipeline runs UseRouting → MapBlazorHub. We have to slot
// authentication/authorization BETWEEN routing and endpoint mapping, which
// the scaffold helper doesn't model, so we replicate its work here inline.
// Keeping the same call order as Sales.WebUI/Program.cs is what makes the
// cookie middleware see the right HttpContext when the Blazor circuit asks
// for AuthenticationStateProvider during prerender.
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
