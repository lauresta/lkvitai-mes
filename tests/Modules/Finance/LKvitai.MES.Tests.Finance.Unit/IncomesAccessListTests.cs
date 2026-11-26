using System.Security.Claims;
using LKvitai.MES.BuildingBlocks.PortalAuth;
using LKvitai.MES.Modules.Finance.Api.Incomes;
using LKvitai.MES.Modules.Finance.WebUI.IncomesReport;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace LKvitai.MES.Tests.Finance.Unit;

public class IncomesAccessListTests
{
    private static IConfiguration Config(params string[] users) => new ConfigurationBuilder()
        .AddInMemoryCollection(users.Select((user, index) =>
            new KeyValuePair<string, string?>($"{IncomesAccessList.ConfigKey}:{index}", user)))
        .Build();

    private static readonly IConfiguration ThreeUsers = Config("bykovas", "dstropiene", "vstropus");

    [Theory]
    [InlineData("bykovas")]
    [InlineData("dstropiene")]
    [InlineData("vstropus")]
    [InlineData("VStropus")]
    public void Listed_users_are_allowed(string username) =>
        Assert.True(IncomesAccessList.IsAllowed(ThreeUsers, username));

    [Theory]
    [InlineData("jjonaitis")]
    [InlineData("admin")]
    [InlineData("d.stropiene")]
    [InlineData("")]
    [InlineData(null)]
    public void Everybody_else_is_denied(string? username) =>
        Assert.False(IncomesAccessList.IsAllowed(ThreeUsers, username));

    [Fact]
    public void An_empty_or_missing_list_lets_nobody_in()
    {
        Assert.False(IncomesAccessList.IsAllowed(new ConfigurationBuilder().Build(), "bykovas"));
        Assert.False(IncomesAccessList.IsAllowed(Config(""), "bykovas"));
        Assert.False(IncomesAccessList.IsAllowed(Config("   "), " "));
        Assert.Empty(IncomesAccessList.Users(Config("", " ")));
    }

    private static ClaimsPrincipal Local(string name) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()), new Claim(ClaimTypes.Name, name)], "Cookies"));

    private static ClaimsPrincipal Sso(string username, string displayName) => new(new ClaimsIdentity(
        [new Claim(ClaimTypes.NameIdentifier, "sso-subject"), new Claim(ClaimTypes.Name, displayName),
         new Claim("auth_source", CikadaJwtValidator.AuthSource), new Claim(CikadaJwtValidator.SsoUsernameClaim, username)], "Cookies"));

    [Fact]
    public void The_page_menu_and_search_use_the_same_list()
    {
        Assert.True(IncomesAccess.IsAllowed(Local("bykovas"), ThreeUsers));
        Assert.True(IncomesAccess.IsAllowed(Sso("dstropiene", "Daiva Stropienė"), ThreeUsers));
        Assert.False(IncomesAccess.IsAllowed(Sso("jjonaitis", "bykovas"), ThreeUsers));
        Assert.False(IncomesAccess.IsAllowed(Local("jjonaitis"), ThreeUsers));
        Assert.False(IncomesAccess.IsAllowed(Local("bykovas"), new ConfigurationBuilder().Build()));
        Assert.False(IncomesAccess.IsAllowed(new ClaimsPrincipal(new ClaimsIdentity()), ThreeUsers));
        Assert.False(IncomesAccess.IsAllowed(null, ThreeUsers));
    }
}
