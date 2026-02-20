namespace LKvitai.MES.Api.Security;

public sealed record DevTokenRequest(string Username, string Password);
public sealed record DevTokenResponse(string Token, DateTimeOffset ExpiresAt);

public interface IDevAuthService
{
    DevTokenResponse? GenerateToken(DevTokenRequest request);
}
