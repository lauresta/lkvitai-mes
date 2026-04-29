using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Portal.WebUI.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Serilog;
using Serilog.Events;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog — mirrors src/Modules/Warehouse/.../Warehouse.Api/Program.cs.
// File sink writes daily-rolled portal-YYYYMMDD.log under /app/logs, which is
// bind-mounted to a single shared /opt/lkvitai-mes/logs on the test/prod hosts.
// Warehouse.Api writes warehouse-YYYYMMDD.log into the same host dir; the
// filename prefix is the only thing distinguishing the two log streams.
const string structuredLogTemplate =
    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [TraceParent:{TraceParent}] [TraceId:{TraceId}] [CorrelationId:{CorrelationId}] [Req:{RequestMethod} {RequestPath}] {Message:lj}{NewLine}{Exception}";

var loggerConfiguration = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .MinimumLevel.Information()
    .Filter.ByExcluding(logEvent => logEvent.Level is LogEventLevel.Debug or LogEventLevel.Verbose)
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: structuredLogTemplate)
    .WriteTo.File(
        "logs/portal-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: structuredLogTemplate);

Log.Logger = loggerConfiguration.CreateLogger();

builder.Host.UseSerilog();

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddPortalCookieAuthentication(builder.Environment, builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddHttpClient("PortalApi", (sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["PortalApi:BaseUrl"] ?? "https://localhost:5011";

    client.BaseAddress = EnsureTrailingSlash(new Uri(baseUrl));
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddHttpClient("WarehouseApi", (sp, client) =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var baseUrl = configuration["WarehouseApi:BaseUrl"] ?? "https://localhost:5001";

    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();

app.UsePathBase(ResolvePathBase(app.Configuration));
app.UsePortalSecureHosting(app.Environment);

app.Use(async (context, next) =>
{
    if (context.Request.PathBase.HasValue && !context.Request.Path.HasValue)
    {
        context.Response.Redirect($"{context.Request.PathBase}/");
        return;
    }

    await next();
});

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate =
        "HTTP request completed. StatusCode={StatusCode}, ElapsedMs={Elapsed:0.0000}";
});

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.Use(async (context, next) =>
{
    if (IsAnonymousPortalPath(context.Request.Path) ||
        context.User.Identity?.IsAuthenticated == true)
    {
        await next();
        return;
    }

    var returnUrl = context.Request.PathBase + context.Request.Path + context.Request.QueryString;
    context.Response.Redirect(BuildLocalUrl(context, "/login.html", $"returnUrl={Uri.EscapeDataString(returnUrl)}"));
});

app.Use(async (context, next) =>
{
    if (!context.Request.Path.StartsWithSegments("/warehouse", StringComparison.OrdinalIgnoreCase))
    {
        await next();
        return;
    }

    var warehouseBaseUrl = ResolveWarehouseWebUiBaseUrl(context);
    var target = BuildModuleRedirectUrl(warehouseBaseUrl, context.Request.Path, context.Request.QueryString);
    context.Response.Redirect(target);
});

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapPost("/auth/login", async (
    HttpContext httpContext,
    IHttpClientFactory factory,
    ILogger<Program> logger,
    CancellationToken cancellationToken) =>
{
    var form = await httpContext.Request.ReadFormAsync(cancellationToken);
    var username = form["username"].ToString();
    var password = form["password"].ToString();
    var returnUrl = NormalizeReturnUrl(form["returnUrl"].ToString());

    var client = factory.CreateClient("WarehouseApi");

    HttpResponseMessage response;
    PortalLoginResponse? login;
    try
    {
        response = await client.PostAsJsonAsync(
            "api/auth/login",
            new PortalLoginRequest(username, password),
            cancellationToken);
    }
    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
    {
        logger.LogError(ex, "Portal login could not reach Warehouse API for {Username}", username);
        return RedirectToLogin(httpContext, returnUrl, "unreachable");
    }

    if ((int)response.StatusCode is 401 or 403)
    {
        logger.LogWarning("Portal login rejected for {Username} (status {Status})", username, (int)response.StatusCode);
        return RedirectToLogin(httpContext, returnUrl, "invalid");
    }

    if (!response.IsSuccessStatusCode)
    {
        logger.LogError(
            "Portal login: Warehouse API returned unexpected status {Status} for {Username}",
            (int)response.StatusCode,
            username);
        return RedirectToLogin(httpContext, returnUrl, "server");
    }

    try
    {
        login = await response.Content.ReadFromJsonAsync<PortalLoginResponse>(cancellationToken: cancellationToken);
    }
    catch (Exception ex) when (ex is System.Text.Json.JsonException or HttpRequestException or TaskCanceledException)
    {
        logger.LogError(ex, "Portal login: failed to read Warehouse API response for {Username}", username);
        return RedirectToLogin(httpContext, returnUrl, "server");
    }

    if (login is null || string.IsNullOrWhiteSpace(login.Token))
    {
        logger.LogError("Portal login: Warehouse API returned an empty token for {Username}", username);
        return RedirectToLogin(httpContext, returnUrl, "server");
    }

    var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, login.UserId.ToString()),
        new(ClaimTypes.Name, login.Username),
        new(ClaimTypes.Email, login.Email),
        new("warehouse_access_token", login.Token)
    };

    foreach (var role in login.Roles)
    {
        claims.Add(new Claim(ClaimTypes.Role, role));
    }

    try
    {
        httpContext.DeleteLegacyPortalAuthCookies();

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
            new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = login.ExpiresAt
            });
    }
    catch (Exception ex) when (ex is System.Security.Cryptography.CryptographicException or InvalidOperationException)
    {
        // DataProtection failure (e.g. unable to persist/read the key ring,
        // missing/locked auth-keys directory). Surface a clear error code
        // instead of letting the exception bubble into UseExceptionHandler,
        // which would re-execute as /Error and trigger the cookie challenge
        // → /login.html?returnUrl=%2FError redirect loop.
        logger.LogError(ex, "Portal login: failed to issue auth cookie for {Username}", login.Username);
        return RedirectToLogin(httpContext, returnUrl, "server");
    }

    logger.LogInformation("Portal login successful for {Username}", login.Username);
    return Results.Redirect(returnUrl);
}).AllowAnonymous();

app.MapPortalLogout();

app.MapBlazorHub();
app.MapFallbackToFile("index.html");

await app.RunAsync();

static IResult RedirectToLogin(HttpContext context, string returnUrl, string error)
{
    var encodedReturn = Uri.EscapeDataString(returnUrl);
    var encodedError = Uri.EscapeDataString(error);
    return Results.Redirect(BuildLocalUrl(context, "/login.html", $"error={encodedError}&returnUrl={encodedReturn}"));
}

static bool IsAnonymousPortalPath(PathString path)
{
    return path.StartsWithSegments("/login.html", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/auth/login", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/auth/logout", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/styles.css", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/script.js", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/favicon.ico", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/_framework", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/_blazor", StringComparison.OrdinalIgnoreCase);
}

static string NormalizeReturnUrl(string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl))
    {
        return "/";
    }

    return Uri.TryCreate(returnUrl, UriKind.Relative, out _) &&
           returnUrl.StartsWith("/", StringComparison.Ordinal) &&
           !returnUrl.StartsWith("//", StringComparison.Ordinal)
        ? returnUrl
        : "/";
}

static PathString ResolvePathBase(IConfiguration configuration)
{
    var configured = configuration["PathBase"];
    return string.IsNullOrWhiteSpace(configured) ? PathString.Empty : new PathString(configured.TrimEnd('/'));
}

static string BuildLocalUrl(HttpContext context, string path, string? query = null)
{
    var localPath = $"{context.Request.PathBase}{path}";
    return string.IsNullOrWhiteSpace(query) ? localPath : $"{localPath}?{query}";
}

static Uri EnsureTrailingSlash(Uri uri)
{
    var builder = new UriBuilder(uri);
    if (!builder.Path.EndsWith("/", StringComparison.Ordinal))
    {
        builder.Path += "/";
    }

    return builder.Uri;
}

static string ResolveWarehouseWebUiBaseUrl(HttpContext context)
{
    var configuration = context.RequestServices.GetRequiredService<IConfiguration>();
    var configured = configuration["WarehouseWebUi:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return configured;
    }

    var host = context.Request.Host.Host;
    if (IsLocalHost(host))
    {
        return "https://localhost:7229";
    }

    var scheme = context.Request.Scheme;
    if (host.StartsWith("portal.", StringComparison.OrdinalIgnoreCase))
    {
        return $"{scheme}://warehouse.{host["portal.".Length..]}";
    }

    if (host.Equals("mes.lauresta.com", StringComparison.OrdinalIgnoreCase))
    {
        return $"{scheme}://warehouse.mes.lauresta.com";
    }

    if (host.Equals("mes-test.lauresta.com", StringComparison.OrdinalIgnoreCase))
    {
        return $"{scheme}://mes-test.lauresta.com/warehouse";
    }

    return $"{scheme}://warehouse.{host}";
}

static bool IsLocalHost(string host)
{
    return host.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
           host.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
           host.Equals("::1", StringComparison.OrdinalIgnoreCase);
}

static string BuildModuleRedirectUrl(string baseUrl, PathString path, QueryString query)
{
    var normalizedBase = baseUrl.TrimEnd('/');
    var remainingPath = path.StartsWithSegments("/warehouse", out var remainder)
        ? remainder.ToString()
        : string.Empty;

    return $"{normalizedBase}{remainingPath}{query}";
}
