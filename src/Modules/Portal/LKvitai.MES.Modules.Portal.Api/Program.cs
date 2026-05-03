using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Portal.Api.Configuration;
using LKvitai.MES.Modules.Portal.Api.Endpoints;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();

builder.Services
    .AddOptions<PortalDashboardOptions>()
    .Bind(builder.Configuration.GetSection(PortalDashboardOptions.SectionName));

// Portal API speaks the shared PortalStructuredBearer scheme, same contract
// Sales.Api / Warehouse.Api use. The Portal cookie path stays a Portal.WebUI
// concern; this API only ever sees the Bearer token forwarded by
// Portal.WebUI's PortalApiAuthHandler. /status remains AllowAnonymous so the
// dashboard can render version metadata even before the user signs in.
builder.Services
    .AddAuthentication(PortalStructuredBearerAuthenticationDefaults.Scheme)
    .AddScheme<AuthenticationSchemeOptions, PortalStructuredBearerAuthenticationHandler>(
        PortalStructuredBearerAuthenticationDefaults.Scheme,
        _ => { });

builder.Services.AddAuthorization(options =>
{
    options.DefaultPolicy = new AuthorizationPolicyBuilder(
            PortalStructuredBearerAuthenticationDefaults.Scheme)
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health");
app.MapPortalApiV1();

await app.RunAsync();
