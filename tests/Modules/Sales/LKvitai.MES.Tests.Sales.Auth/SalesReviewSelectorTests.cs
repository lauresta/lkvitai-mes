using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using LKvitai.MES.Modules.Sales.Api.Security;
using LKvitai.MES.Modules.Sales.WebUI.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class SalesReviewSelectorTests
{
    [Theory]
    [InlineData("valid", "absent", true, "portal")]
    [InlineData("valid", "valid", true, "review")]
    [InlineData("valid", "expired", true, "portal")]
    [InlineData("valid", "tampered", true, "portal")]
    [InlineData("valid", "wrong-key", true, "portal")]
    [InlineData("valid", "invalid-identity", true, "portal")]
    [InlineData("valid", "valid", false, "portal")]
    [InlineData("expired", "valid", true, "review")]
    [InlineData("tampered", "valid", true, "review")]
    [InlineData("absent", "valid", true, "review")]
    [InlineData("expired", "expired", true, "anonymous")]
    [InlineData("expired", "tampered", true, "anonymous")]
    [InlineData("tampered", "wrong-key", true, "anonymous")]
    [InlineData("absent", "expired", true, "anonymous")]
    [InlineData("absent", "tampered", true, "anonymous")]
    [InlineData("absent", "wrong-key", true, "anonymous")]
    [InlineData("absent", "valid", false, "anonymous")]
    [InlineData("absent", "absent", true, "anonymous")]
    public async Task Only_a_valid_review_session_takes_priority_over_the_native_portal_cookie(
        string portalMode, string reviewMode, bool reviewEnabled, string expected)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Environment.EnvironmentName = "Test";
        var key = new string('a', 64);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SalesReview:Key"] = key,
            ["SalesReview:ExpiresAtUtc"] = DateTimeOffset.UtcNow.AddHours(reviewEnabled ? 1 : -1).ToString("O")
        });
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("Cookies").AddCookie("Cookies");
        builder.Services.AddSalesReviewAccess(builder.Environment, builder.Configuration);
        builder.Services.AddSalesSessionCredentials();
        builder.Services.AddSalesProtectedAuthentication();
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapSalesSessionProof();
        app.MapGet("/selected", (HttpContext context) =>
        {
            var identityCount = context.User.Identities.Count(identity => identity.IsAuthenticated);
            var selected = identityCount == 0 ? "anonymous" : context.User.HasClaim(SalesReviewAccess.ReviewClaim, "true") ? "review" : "portal";
            // A result with both identities would violate review isolation.
            return Results.Text($"{selected}:{identityCount}");
        });
        app.MapGet("/api/sales/actor", () => Results.Ok())
            .RequireAuthorization(LKvitai.MES.BuildingBlocks.PortalAuth.SalesCredentialProtector.Policy);
        await app.StartAsync();
        var client = app.GetTestClient();
        var cookies = new List<string>();
        var portalOptions = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        var reviewOptions = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get(SalesReviewAccess.CookieScheme);
        if (portalMode != "absent")
        {
            var portalClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Name, "Portal test user"),
                new Claim("warehouse_access_token", "native-portal-token")
            };
            cookies.Add(portalOptions.Cookie.Name + "=" + (portalMode == "tampered" ? "invalid-cookie" : portalOptions.TicketDataFormat.Protect(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(portalClaims, "Cookies")),
                    Properties(portalMode == "expired"), "Cookies"))));
        }
        if (reviewMode != "absent")
        {
            var reviewClaims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "sales-review"),
                new Claim(ClaimTypes.Name, "Design review"),
                new Claim(SalesReviewAccess.ReviewClaim, reviewMode == "invalid-identity" ? "false" : "true"),
                new Claim("sales_review_key_id", Convert.ToHexString(SHA256.HashData(Convert.FromHexString(reviewMode == "wrong-key" ? new string('b',64) : key))))
            };
            cookies.Add(SalesReviewAccess.CookieName + "=" + (reviewMode == "tampered" ? "invalid-review-cookie" : reviewOptions.TicketDataFormat.Protect(
                new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(reviewClaims, SalesReviewAccess.CookieScheme)),
                    Properties(reviewMode == "expired"), SalesReviewAccess.CookieScheme))));
        }
        if (cookies.Count > 0) client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies));
        Assert.Equal(expected + (expected == "anonymous" ? ":0" : ":1"), await client.GetStringAsync("/selected"));
        Assert.Equal(expected switch { "review" => HttpStatusCode.Forbidden, "portal" => HttpStatusCode.OK, _ => HttpStatusCode.Unauthorized },
            (await client.GetAsync("/auth/session-proof")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/sales/actor")).StatusCode);
    }

    private static AuthenticationProperties Properties(bool expired) => new()
    {
        IssuedUtc = DateTimeOffset.UtcNow.AddMinutes(-10),
        ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(expired ? -5 : 5)
    };
}
