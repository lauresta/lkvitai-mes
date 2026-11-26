namespace LKvitai.MES.Modules.Finance.Api.Incomes;

/// <summary>
/// Who may open Finance → Įplaukų judėjimas: Cikada usernames (the SSO username) listed under
/// <c>Finance:Incomes:AllowedUsers</c> (env <c>Finance__Incomes__AllowedUsers__0..n</c>). The list fails closed:
/// missing or empty means nobody, including other Finance users. The codebase has no global-admin role,
/// so administrators are listed explicitly like everyone else.
/// </summary>
public static class IncomesAccessList
{
    public const string ConfigKey = "Finance:Incomes:AllowedUsers";

    public static IReadOnlyList<string> Users(IConfiguration configuration) =>
        (configuration.GetSection(ConfigKey).Get<string[]>() ?? [])
        .Select(value => value?.Trim() ?? "")
        .Where(value => value.Length > 0)
        .ToArray();

    public static bool IsAllowed(IConfiguration configuration, string? username) =>
        !string.IsNullOrWhiteSpace(username) &&
        Users(configuration).Any(value => string.Equals(value, username.Trim(), StringComparison.OrdinalIgnoreCase));
}
