using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
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

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!app.Environment.IsDevelopment() && !string.Equals(app.Environment.EnvironmentName, "Test", StringComparison.OrdinalIgnoreCase))
{
    app.UseHttpsRedirection();
}

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
    context.Response.Redirect($"/login.html?returnUrl={Uri.EscapeDataString(returnUrl)}");
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
        return Results.Redirect($"/login.html?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
    }

    var login = await response.Content.ReadFromJsonAsync<PortalLoginResponse>(cancellationToken: cancellationToken);
    if (login is null || string.IsNullOrWhiteSpace(login.Token))
    {
        logger.LogWarning("Portal login failed because API returned an empty token for {Username}", username);
        return Results.Redirect($"/login.html?error=invalid&returnUrl={Uri.EscapeDataString(returnUrl)}");
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

app.MapPost("/auth/logout", async (HttpContext httpContext) =>
{
    await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    return Results.Redirect("/login.html");
});

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
        return $"{scheme}://warehouse.mes-test.lauresta.com";
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

public sealed record PortalLoginRequest(string Username, string Password);

public sealed record PortalLoginResponse(
    string Token,
    DateTimeOffset ExpiresAt,
    Guid UserId,
    string Username,
    string Email,
    IReadOnlyList<string> Roles);
