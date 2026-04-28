using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Portal.WebUI.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

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
    var response = await client.PostAsJsonAsync(
        "api/auth/login",
        new PortalLoginRequest(username, password),
        cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        logger.LogWarning("Portal login rejected for {Username}", username);
        return Results.Redirect(BuildLocalUrl(httpContext, "/login.html", $"error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}"));
    }

    var login = await response.Content.ReadFromJsonAsync<PortalLoginResponse>(cancellationToken: cancellationToken);
    if (login is null || string.IsNullOrWhiteSpace(login.Token))
    {
        logger.LogWarning("Portal login failed because API returned an empty token for {Username}", username);
        return Results.Redirect(BuildLocalUrl(httpContext, "/login.html", $"error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}"));
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

    await httpContext.SignInAsync(
        CookieAuthenticationDefaults.AuthenticationScheme,
        new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
        new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = login.ExpiresAt
        });

    logger.LogInformation("Portal login successful for {Username}", login.Username);
    return Results.Redirect(returnUrl);
}).AllowAnonymous();

app.MapPortalLogout();

app.MapBlazorHub();
app.MapFallbackToFile("index.html");

await app.RunAsync();

static bool IsAnonymousPortalPath(PathString path)
{
    return path.StartsWithSegments("/login.html", StringComparison.OrdinalIgnoreCase) ||
           path.StartsWithSegments("/auth/login", StringComparison.OrdinalIgnoreCase) ||
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
