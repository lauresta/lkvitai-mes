using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddDataProtection()
    .SetApplicationName("LKvitai.MES.PortalAuth")
    .PersistKeysToFileSystem(new DirectoryInfo(GetDataProtectionKeysPath(builder.Environment, builder.Configuration)));
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/login.html";
        options.AccessDeniedPath = "/access-denied";
        options.Cookie.Name = "LKvitai.MES.Portal";
        options.Cookie.Domain = ResolveCookieDomain(builder.Configuration);
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddTransient<WarehouseApiAuthHandler>();

builder.Services.AddHttpClient("WarehouseApi", (sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["WarehouseApi:BaseUrl"] ?? "https://localhost:5001";

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
})
.AddHttpMessageHandler<WarehouseApiAuthHandler>();

builder.Services.AddScoped<DashboardClient>();
builder.Services.AddScoped<StockClient>();
builder.Services.AddScoped<ReservationsClient>();
builder.Services.AddScoped<ProjectionsClient>();
builder.Services.AddScoped<MasterDataAdminClient>();
builder.Services.AddScoped<ReportsClient>();
builder.Services.AddScoped<OutboundClient>();
builder.Services.AddScoped<SalesOrdersClient>();
builder.Services.AddScoped<ReceivingClient>();
builder.Services.AddScoped<TransfersClient>();
builder.Services.AddScoped<ValuationClient>();
builder.Services.AddScoped<AgnumClient>();
builder.Services.AddScoped<AdminUsersClient>();
builder.Services.AddScoped<AdminConfigurationClient>();
builder.Services.AddScoped<ApiKeysClient>();
builder.Services.AddScoped<GdprClient>();
builder.Services.AddScoped<AuditLogsClient>();
builder.Services.AddScoped<BackupsClient>();
builder.Services.AddScoped<RetentionPoliciesClient>();
builder.Services.AddScoped<DisasterRecoveryClient>();
builder.Services.AddScoped<SerialNumbersClient>();
builder.Services.AddScoped<LayoutEditorClient>();
builder.Services.AddScoped<LotsClient>();
builder.Services.AddScoped<UomClient>();
builder.Services.AddScoped<VisualizationClient>();
builder.Services.AddScoped<CycleCountsClient>();
builder.Services.AddScoped<AdvancedWarehouseClient>();
builder.Services.AddScoped<AdjustmentsClient>();
builder.Services.AddScoped<PutawayClient>();
builder.Services.AddScoped<PickingTasksClient>();
builder.Services.AddScoped<LabelsClient>();
builder.Services.AddScoped<ToastService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment() && !string.Equals(app.Environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (IsDevAuthEnabled(context) &&
        context.User.Identity?.IsAuthenticated != true)
    {
        context.User = BuildDevPrincipal();
    }

    await next();
});
app.UseAuthorization();

app.MapPost("/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login.html");
});

// Photo proxy: in production nginx routes /api/* to the API directly.
// In local dev the browser resolves relative /api/* URLs against the WebUI port,
// so this endpoint forwards those requests to the backend API and streams the response.
app.MapGet("/api/warehouse/v1/items/{itemId:int}/photos/{photoId:guid}",
    async (int itemId, Guid photoId, string? size, IHttpClientFactory factory,
           HttpContext httpCtx, CancellationToken cancellationToken) =>
    {
        var client = factory.CreateClient("WarehouseApi");
        var sizeParam = string.IsNullOrWhiteSpace(size) ? "thumb" : size;
        var upstream = await client.GetAsync(
            $"/api/warehouse/v1/items/{itemId}/photos/{photoId}?size={sizeParam}",
            cancellationToken);

        httpCtx.Response.StatusCode = (int)upstream.StatusCode;

        if (upstream.Content.Headers.ContentType is { } contentType)
            httpCtx.Response.ContentType = contentType.ToString();

        if (upstream.Headers.ETag is { } etag)
            httpCtx.Response.Headers.ETag = etag.ToString();

        if (upstream.Headers.CacheControl is { } cacheControl)
            httpCtx.Response.Headers.CacheControl = cacheControl.ToString();

        if (upstream.IsSuccessStatusCode)
            await upstream.Content.CopyToAsync(httpCtx.Response.Body, cancellationToken);
    });

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

static string GetDataProtectionKeysPath(IHostEnvironment environment, IConfiguration configuration)
{
    var configured = configuration["PortalAuth:DataProtectionKeysPath"];
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return configured;
    }

    return Path.GetFullPath(Path.Combine(environment.ContentRootPath, "../../../../.data-protection-keys"));
}

static string? ResolveCookieDomain(IConfiguration configuration)
{
    var configured = configuration["PortalAuth:CookieDomain"];
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return configured;
    }

    return null;
}

static bool IsDevAuthEnabled(HttpContext context)
{
    var environment = context.RequestServices.GetRequiredService<IHostEnvironment>();
    if (!environment.IsDevelopment() &&
        !string.Equals(environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
    {
        return false;
    }

    var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
    return configuration.GetValue<bool>("WarehouseWebUi:DevAuthEnabled");
}

static ClaimsPrincipal BuildDevPrincipal()
{
    const string roles = "Operator,QCInspector,WarehouseManager,WarehouseAdmin,InventoryAccountant,CFO";
    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, "dev-user"),
        new(ClaimTypes.Name, "dev-user"),
        new("warehouse_access_token", $"dev-user|{roles}")
    };

    foreach (var role in roles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }

    return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
}
