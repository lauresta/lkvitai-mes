using LKvitai.MES.Application.Services;
using System.Security.Claims;

namespace LKvitai.MES.Api.Services;

public sealed class HttpContextCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpContextCurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            return "system";
        }

        return user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? user.Identity?.Name
            ?? "system";
    }
}
