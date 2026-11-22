using System.Net.Http;
using System.Security.Claims;
using System.Text;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum;
using LKvitai.MES.Modules.Warehouse.WebUI.Infrastructure;
using LKvitai.MES.Modules.Warehouse.WebUI.Models;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Putaway destinations, actor names, plural forms, Intrastat item check and Agnum response decoding.</summary>
public sealed class WalkthroughFixesTests
{
    private static readonly string[] VirtualCodes =
    [
        "CUSTOMER", "IN_TRANSIT_TRF-1", "PRODUCTION", "QC_HOLD", "QUARANTINE", "RETURN_TO_SUPPLIER", "SCRAP", "SHIPPING",
        "WAREHOUSE_MERGE", "RECEIVING", "AGNUM", "SUPPLIER"
    ];

    [Theory]
    [InlineData("A-01-01", false, true)]
    [InlineData("a-01-01", false, true)]
    [InlineData("A-01-01", true, false)]
    public void StorageLocation_IsAPhysicalCell(string code, bool isVirtual, bool expected)
        => PhysicalLocation.IsStorageLocation(new Location { Code = code, IsVirtual = isVirtual }).Should().Be(expected);

    [Fact]
    public void StorageLocation_RejectsEveryVirtualCode_EvenWhenNotFlaggedVirtual()
    {
        foreach (var code in VirtualCodes)
        {
            PhysicalLocation.IsStorageLocation(new Location { Code = code, IsVirtual = false })
                .Should().BeFalse($"{code} is not a storage cell");
        }

        PhysicalLocation.IsStorageLocation(null).Should().BeFalse();
    }

    [Fact]
    public void PutawaySelect_AgreesWithTheServerRule()
    {
        var codes = VirtualCodes.Append("A-01-01").Append("B-12-03").Append("in_transit_9").Append("qc_hold");
        foreach (var code in codes)
        {
            foreach (var isVirtual in new[] { false, true })
            {
                var server = PhysicalLocation.IsStorageLocation(new Location { Code = code, IsVirtual = isVirtual });
                var ui = PutawayDestinations.IsStorage(new AdminLocationDto { Code = code, IsVirtual = isVirtual });
                ui.Should().Be(server, $"{code} virtual={isVirtual}");
            }
        }
    }

    [Fact]
    public void ActorName_PrefersTheDisplayNameOverTheSubjectId()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "9f86d081884c7d659a2feaa0c55ad015a3bf4f1b2b0b822cd15d6c15b0f00a08"),
             new Claim("preferred_username", "jonas.jonaitis"), new Claim("name", "Jonas Jonaitis")], "test"));

        ActorName.Resolve(user, "9f86d081884c7d65").Should().Be("Jonas Jonaitis");
    }

    [Fact]
    public void ActorName_FallsBackThroughUsernameAndEmailToTheId()
    {
        ActorName.Resolve(new ClaimsPrincipal(new ClaimsIdentity([new Claim("preferred_username", "jonas")], "test")), "id-1")
            .Should().Be("jonas");
        ActorName.Resolve(new ClaimsPrincipal(new ClaimsIdentity([new Claim("email", "j@example.test")], "test")), "id-1")
            .Should().Be("j@example.test");
        ActorName.Resolve(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "id-1")], "test")), "id-1")
            .Should().Be("id-1");
        ActorName.Resolve(null, "system").Should().Be("system");
    }

    [Fact]
    public void ActorLabel_ShortensAnOpaqueId_AndKeepsAName()
    {
        string Unknown(string tail) => "User …" + tail;

        ActorLabel.Display("Jonas Jonaitis", Unknown).Should().Be("Jonas Jonaitis");
        ActorLabel.Display("jonas.jonaitis", Unknown).Should().Be("jonas.jonaitis");
        ActorLabel.Display("3f2504e0-4f89-11d3-9a0c-0305e82c3301", Unknown).Should().Be("User …2c3301");
        ActorLabel.Display("kP9xQ2mVb7LrT4nWz8YcHd3Jf6Ag", Unknown).Should().StartWith("User …");
        ActorLabel.Display(null, Unknown).Should().Be("-");
        ActorLabel.Display("  ", Unknown).Should().Be("-");
    }

    [Theory]
    [InlineData(1, "en", "One")]
    [InlineData(0, "en", "Other")]
    [InlineData(2, "en", "Other")]
    [InlineData(11, "en", "Other")]
    [InlineData(1, "lt", "One")]
    [InlineData(21, "lt", "One")]
    [InlineData(31, "lt", "One")]
    [InlineData(2, "lt", "Few")]
    [InlineData(9, "lt", "Few")]
    [InlineData(22, "lt", "Few")]
    [InlineData(10, "lt", "Many")]
    [InlineData(11, "lt", "Many")]
    [InlineData(19, "lt", "Many")]
    [InlineData(20, "lt", "Many")]
    [InlineData(0, "lt", "Many")]
    [InlineData(111, "lt", "Many")]
    [InlineData(101, "lt", "One")]
    [InlineData(1, null, "One")]
    public void PluralForm_FollowsTheLanguageRules(int count, string? language, string expected)
        => PluralForm.Suffix(count, language).Should().Be(expected);

    [Theory]
    [InlineData("Stock", "84713000", 0.5, "CN", false)]
    [InlineData("Stock", null, 0.5, "CN", true)]
    [InlineData("Stock", "84713000", null, "CN", true)]
    [InlineData("Stock", "84713000", 0.0, "CN", true)]
    [InlineData("Stock", "84713000", 0.5, " ", true)]
    [InlineData("Service", null, null, null, false)]
    [InlineData("service", null, null, null, false)]
    public void IntrastatItemCheck_FlagsOnlyStockItemsWithGaps(string type, string? code, double? weight, string? origin, bool expected)
        => IntrastatItemCheck.IsDataMissing(type, code, weight is null ? null : (decimal)weight, origin).Should().Be(expected);

    [Fact]
    public void AgnumResponse_Utf8IsReadAsIs()
    {
        var text = "[{\"category\":\"Prekės\"}]";

        AgnumResponseText.Decode(Encoding.UTF8.GetBytes(text)).Should().Be(text);
        AgnumResponseText.Decode([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(text)]).Should().Be(text);
    }

    [Fact]
    public void AgnumResponse_WindowsBalticBytes_AreNotTurnedIntoReplacementCharacters()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = "[{\"category\":\"Prekės ąčęėįšųūž\"}]";
        var bytes = Encoding.GetEncoding(1257).GetBytes(text);

        var decoded = AgnumResponseText.Decode(bytes);

        decoded.Should().Be(text);
        decoded.Should().NotContain("�");
    }

    [Fact]
    public async Task AgnumResponse_IgnoresAnUnknownCharsetHeader()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = "Prekės";
        using var content = new ByteArrayContent(Encoding.GetEncoding(1257).GetBytes(text));
        content.Headers.TryAddWithoutValidation("Content-Type", "application/json; charset=x-not-a-charset");

        (await AgnumResponseText.ReadAsync(content, CancellationToken.None)).Should().Be(text);
    }
}
