using System.Net.Http.Headers;
using LKvitai.MES.Modules.Warehouse.WebUI.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

builder.Services.AddHttpClient("WarehouseApi", (sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["WarehouseApi:BaseUrl"] ?? "https://localhost:5001";
    var userId = configuration["WarehouseApi:UserId"] ?? "webui-admin";
    var roles = configuration["WarehouseApi:Roles"] ?? "WarehouseAdmin,WarehouseManager,QCInspector,Operator";

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Accept.Clear();
    client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    client.DefaultRequestHeaders.Remove("X-User-Id");
    client.DefaultRequestHeaders.Remove("X-User-Roles");
    client.DefaultRequestHeaders.Add("X-User-Id", userId);
    client.DefaultRequestHeaders.Add("X-User-Roles", roles);
});

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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
