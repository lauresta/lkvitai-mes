using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.UseScaffoldSerilog("scanning");
builder.Services.AddScaffoldApiCore();
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddCikadaSsoTokenValidation(builder.Configuration);
builder.Services.AddAuthentication()
    .AddScheme<AuthenticationSchemeOptions, PortalStructuredBearerAuthenticationHandler>(
        PortalStructuredBearerAuthenticationDefaults.Scheme, _ => { });
builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
        CookieAuthenticationDefaults.AuthenticationScheme,
        PortalStructuredBearerAuthenticationDefaults.Scheme)
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseScaffoldApiPipeline();
app.UseAuthentication();
app.UseAuthorization();

// All future scanner data and commands inherit Portal/SSO authentication.
// Their handlers must also enforce the mapped legacy identity and row ACL.
var scanning = app.MapGroup("/api/scan").RequireAuthorization();

scanning.MapGet("/ping", () => Results.Ok(new
{
    Module = "Scanning",
    Now = DateTimeOffset.UtcNow.ToString("O")
})).AllowAnonymous(); // Scaffold health probe has no business data.

await app.RunAsync();
