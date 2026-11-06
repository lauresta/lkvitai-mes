using LKvitai.MES.BuildingBlocks.ModuleStartup;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Finance.Api.BankPayments;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.UseScaffoldSerilog("finance-api");
builder.Services.AddScaffoldApiCore();
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.Configure<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
});
builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<CikadaUserBridgeProof>();
builder.Services.AddHttpClient("CikadaUsers", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["CikadaUsers:BaseUrl"] ?? "http://localhost:5001");
    client.Timeout = TimeSpan.FromSeconds(10);
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
builder.Services.AddScoped<FinanceAccess>();
builder.Services.AddScoped<BankRepository>();
builder.Services.AddSingleton<BankImportSessions>();
var app = builder.Build();
app.UseScaffoldApiPipeline();
app.UseAuthentication();
app.UseAuthorization();
app.MapBankPayments();
await app.RunAsync();
