using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Claims;
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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace LKvitai.MES.Tests.Sales.Auth;

public sealed class SalesAuthDiagnosticsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("invalid", false)]
    [InlineData("2026-11-01T12:00:00Z", false)]
    [InlineData("2026-11-01T11:59:59Z", false)]
    [InlineData("2026-11-01T12:30:00Z", true)]
    [InlineData("2026-11-01T13:00:00Z", true)]
    [InlineData("2026-11-01T13:00:01Z", false)]
    public void Only_a_future_deadline_within_one_hour_enables_logging(string? deadline, bool enabled)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { [SalesAuthDiagnostics.ExpiresAtConfigurationKey] = deadline }).Build();
        Assert.Equal(enabled, SalesAuthDiagnostics.IsEnabled(configuration, DateTimeOffset.Parse("2026-11-01T12:00:00Z", CultureInfo.InvariantCulture)));
    }

    [Fact]
    public async Task Enabled_diagnostics_observe_authentication_without_logging_secrets_or_changing_it()
    {
        var logs = new CaptureLogs();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        { [SalesAuthDiagnostics.ExpiresAtConfigurationKey] = DateTimeOffset.UtcNow.AddMinutes(5).ToString("O") });
        builder.Logging.AddProvider(logs);
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", options => options.Cookie.Name = "LKvitai.MES.Portal");
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseSalesAuthDiagnostics();
        app.MapGet("/", (HttpContext context) => Results.Text(context.User.Identity?.IsAuthenticated == true ? "authenticated" : "anonymous"));
        app.MapGet("/_blazor", () => Results.Ok());
        app.MapPost("/_blazor/negotiate", () => Results.Ok());
        app.MapGet("/other", () => Results.Ok());
        await app.StartAsync();
        var client = app.GetTestClient();
        Assert.Equal("anonymous", await client.GetStringAsync("/?secret=query-secret"));
        Assert.Contains(logs.Messages, message => message.Contains("PortalCookiePresent=False") && message.Contains("CookieResult=None"));

        var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>().Get("Cookies");
        // The native portal cookie contains identity and Warehouse access-token
        // claims. Diagnostic output must never include their values.
        var subject = Guid.NewGuid();
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, subject.ToString()),
            new Claim(ClaimTypes.Name, "private-user-name"),
            new Claim("warehouse_access_token", "private-access-token"),
            new Claim("sales_portal_session_proof", "private-session-proof")
        };
        var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, "Cookies")),
            new AuthenticationProperties { ExpiresUtc = DateTimeOffset.UtcNow.AddMinutes(10) }, "Cookies");
        var protectedCookie = options.TicketDataFormat.Protect(ticket);
        client.DefaultRequestHeaders.Add("Cookie", "LKvitai.MES.Portal=" + protectedCookie);
        Assert.Equal("authenticated", await client.GetStringAsync("/"));
        Assert.Contains(logs.Messages, message => message.Contains("CookieResult=Success") && message.Contains("TicketSubjectIsGuid=True"));
        await client.GetAsync("/_blazor");
        await client.PostAsync("/_blazor/negotiate", new StringContent("private-request-body"));
        var count = logs.Messages.Count;
        await client.GetAsync("/other");
        Assert.Equal(count, logs.Messages.Count);

        client.DefaultRequestHeaders.Remove("Cookie");
        client.DefaultRequestHeaders.Add("Cookie", "LKvitai.MES.Portal=private-invalid-cookie");
        Assert.Equal("anonymous", await client.GetStringAsync("/"));
        Assert.Contains(logs.Messages, message => message.Contains("FailureReason=UnprotectTicketFailed"));
        var joined = string.Join('\n', logs.Messages);
        foreach (var forbidden in new[] { subject.ToString(), "private-user-name", "private-access-token", "private-session-proof", protectedCookie, "private-invalid-cookie", "query-secret", "private-request-body" })
            Assert.DoesNotContain(forbidden, joined);
    }

    private sealed class CaptureLogs : ILoggerProvider
    {
        public ConcurrentQueue<string> Messages { get; } = new();
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(categoryName, Messages);
        public void Dispose() { }
        private sealed class CaptureLogger(string category, ConcurrentQueue<string> messages) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (category == "Sales.AuthDiagnostics") messages.Enqueue(formatter(state, exception));
            }
        }
    }
}
