using System.Globalization;
using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LKvitai.MES.Modules.Sales.WebUI.Services;

/// <summary>
/// Default-off, bounded diagnostics for native Portal-to-Sales login failures.
/// Logs result metadata only: never cookies, tickets, proofs, claim values or
/// raw exception messages. No routes or authentication decisions are added.
/// </summary>
public static class SalesAuthDiagnostics
{
    public const string ExpiresAtConfigurationKey = "SalesAuthDiagnostics:ExpiresAtUtc";

    public static IApplicationBuilder UseSalesAuthDiagnostics(this IApplicationBuilder app)
        => app.Use(async (context, next) =>
        {
            var clock = context.RequestServices.GetService<TimeProvider>() ?? TimeProvider.System;
            if (IsEnabled(context.RequestServices.GetRequiredService<IConfiguration>(), clock.GetUtcNow())
                && IsDiagnosticRequest(context.Request))
            {
                var logger = context.RequestServices.GetRequiredService<ILoggerFactory>()
                    .CreateLogger("Sales.AuthDiagnostics");
                try
                {
                    // Cookie authentication caches its result for this request.
                    // Explicit scheme selection only observes the Portal result;
                    // it never replaces the selected context/review principal.
                    var result = await context.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    var identities = context.User.Identities.Where(identity => identity.IsAuthenticated).ToArray();
                    var ticketIdentity = result.Principal?.Identities.FirstOrDefault(identity => identity.IsAuthenticated);
                    var subjectIsGuid = Guid.TryParse(ticketIdentity?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var subject)
                        && subject != Guid.Empty;
                    logger.LogWarning(
                        "Sales auth diagnostic: Path={DiagnosticPath}, PortalCookiePresent={PortalCookiePresent}, CookieResult={CookieResult}, FailureReason={FailureReason}, FailureType={FailureType}, ContextAuthenticated={ContextAuthenticated}, AuthenticatedIdentityCount={AuthenticatedIdentityCount}, ContextAuthenticationType={ContextAuthenticationType}, TicketSchemeIsCookies={TicketSchemeIsCookies}, TicketExpiresUtc={TicketExpiresUtc}, TicketSubjectIsGuid={TicketSubjectIsGuid}",
                        context.Request.Path.Value,
                        context.Request.Cookies.ContainsKey(PortalAuthDefaults.CookieName),
                        result.Succeeded ? "Success" : result.None ? "None" : "Failure",
                        SafeFailureReason(result.Failure),
                        result.Failure?.GetType().Name,
                        context.User.Identity?.IsAuthenticated == true,
                        identities.Length,
                        identities.Length == 0 ? "None" : identities.All(identity => identity.AuthenticationType == CookieAuthenticationDefaults.AuthenticationScheme) ? "Cookies" : "Other",
                        result.Ticket?.AuthenticationScheme == CookieAuthenticationDefaults.AuthenticationScheme,
                        result.Properties?.ExpiresUtc,
                        subjectIsGuid);
                }
                catch (Exception exception)
                {
                    // Diagnostics must not turn an otherwise valid request into
                    // a failed login. Deliberately omit exception and message.
                    logger.LogWarning("Sales auth diagnostic failed: FailureType={FailureType}", exception.GetType().Name);
                }
            }
            await next(context);
        });

    public static bool IsEnabled(IConfiguration configuration, DateTimeOffset now)
        => DateTimeOffset.TryParse(configuration[ExpiresAtConfigurationKey], CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var expires)
            && expires > now && expires <= now.AddHours(1);

    private static bool IsDiagnosticRequest(HttpRequest request)
        => HttpMethods.IsGet(request.Method) && (request.Path == "/" || request.Path == "/_blazor")
            || HttpMethods.IsPost(request.Method) && request.Path == "/_blazor/negotiate";

    private static string SafeFailureReason(Exception? failure) => failure switch
    {
        null => "None",
        { Message: "Unprotect ticket failed" } => "UnprotectTicketFailed",
        { Message: "Ticket expired" } => "TicketExpired",
        _ => "Other"
    };
}
