using LKvitai.MES.BuildingBlocks.WebUI.Localization;
using System.Net.Http.Headers;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.BuildingBlocks.WebUI.Services;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddCikadaLocalization(builder.Configuration);
builder.Services.AddMudServices();
builder.Services.AddHttpContextAccessor();
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddBuildVersion();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CikadaUserBridgeProof>();
if (builder.Configuration.IsCikadaSsoEnabled())
{
    builder.Services.AddCikadaSsoTokenValidation(builder.Configuration);
    builder.Services.AddSingleton<CikadaSsoAccountProof>();
    builder.Services.AddScoped<CikadaSsoAccountLookup>();
    builder.Services.AddHttpClient("CikadaUsers", client =>
    {
        client.BaseAddress = new Uri(builder.Configuration["WarehouseApi:BaseUrl"] ?? "https://localhost:5001");
        client.Timeout = TimeSpan.FromSeconds(10);
    }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
}
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
builder.Services.AddScoped<ItemPricesClient>();
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
builder.Services.AddHttpClient("WarehouseUserAdminApi", (sp, client)=>
{
    client.BaseAddress=new Uri(sp.GetRequiredService<IConfiguration>()["WarehouseApi:BaseUrl"] ?? "https://localhost:5001");
    client.Timeout=TimeSpan.FromSeconds(30);
}).ConfigurePrimaryHttpMessageHandler(()=>new HttpClientHandler{AllowAutoRedirect=false});
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

app.UsePathBase(ResolvePathBase(app.Configuration));
// Shared EN/LT UI culture from the cross-module culture cookie (docs/i18n/README.md).
app.UseCikadaRequestLocalization();
app.UsePortalSecureHosting(app.Environment);

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseWarehouseDevAuth();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok());

app.MapPortalLogout();

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

app.MapCikadaCultureEndpoint();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();

static PathString ResolvePathBase(IConfiguration configuration)
{
    var configured = configuration["PathBase"];
    return string.IsNullOrWhiteSpace(configured) ? PathString.Empty : new PathString(configured.TrimEnd('/'));
}
