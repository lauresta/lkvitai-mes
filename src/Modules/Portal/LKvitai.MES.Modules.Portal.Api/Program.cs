using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Portal.Api;
using LKvitai.MES.Modules.Portal.Api.Configuration;
using LKvitai.MES.Modules.Portal.Api.Endpoints;
using LKvitai.MES.Modules.Portal.Api.Persistence;
using LKvitai.MES.Modules.Portal.Api.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddMemoryCache();
builder.Services.AddHttpClient("GitHubReleases", client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("LKvitai.MES.Portal/1.0");
    client.Timeout = TimeSpan.FromSeconds(12);
});
builder.Services.AddScoped<GitHubReleaseNewsService>();

var portalDbConnectionString = builder.Configuration.GetConnectionString("PortalDb");
if (string.IsNullOrWhiteSpace(portalDbConnectionString))
{
    throw new InvalidOperationException("ConnectionStrings:PortalDb is required for Portal CMS.");
}
var portalDbBuilder = new NpgsqlConnectionStringBuilder(portalDbConnectionString)
{
    MinPoolSize = 2,
    MaxPoolSize = 25,
    ConnectionLifetime = 300,
    ConnectionIdleLifetime = 60,
    Timeout = 30
};
builder.Services.AddDbContext<PortalDbContext>(options =>
{
    options.UseNpgsql(portalDbBuilder.ConnectionString, npgsql =>
    {
        npgsql.MigrationsHistoryTable("__EFMigrationsHistory", PortalDbContext.Schema);
        npgsql.EnableRetryOnFailure(maxRetryCount: 3);
    });
});

builder.Services
    .AddOptions<PortalDashboardOptions>()
    .Bind(builder.Configuration.GetSection(PortalDashboardOptions.SectionName));

// LKvitaiDb (SQL Server) — optional; used by SqlOperationsSummaryService.
// Portal.Api starts normally even when the connection string is absent:
// the operations-summary endpoint returns a null/empty response instead
// of failing with a startup exception.
var salesConnStr = builder.Configuration.GetConnectionString("LKvitaiDb");
builder.Services.AddSingleton(new SalesDbOptions
{
    ConnectionString = salesConnStr,
    CommandTimeoutSeconds = builder.Configuration
        .GetValue("Sales:Sql:CommandTimeoutSeconds", 30)
});
builder.Services.AddScoped<SqlOperationsSummaryService>();

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

    options.AddPolicy(PortalPolicies.AdminOnly, policy =>
        policy.RequireRole(PortalPolicies.AdminRoles));
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

await PortalDbSeeder.SeedAsync(app.Services);

await app.RunAsync();
