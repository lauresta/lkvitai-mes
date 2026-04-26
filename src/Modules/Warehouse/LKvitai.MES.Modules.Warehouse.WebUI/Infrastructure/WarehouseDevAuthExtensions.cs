using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;

public static class WarehouseDevAuthExtensions
{
    private const string DevUserId = "dev-user";
    private const string DevRoles = "Operator,QCInspector,WarehouseManager,WarehouseAdmin,InventoryAccountant,CFO";

    public static IApplicationBuilder UseWarehouseDevAuth(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            if (IsEnabled(context) && context.User.Identity?.IsAuthenticated != true)
            {
                context.User = BuildPrincipal();
            }

            await next();
        });
    }

    private static bool IsEnabled(HttpContext context)
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

    private static ClaimsPrincipal BuildPrincipal()
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, DevUserId),
            new(ClaimTypes.Name, DevUserId),
            new("warehouse_access_token", $"{DevUserId}|{DevRoles}")
        };

        foreach (var role in DevRoles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }
}
