using System.Net;
using System.Text;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Integration.Agnum;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class AgnumAccountingW3Tests
{
    private static WarehouseDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new WarehouseDbContext(options);
    }

    private static AgnumScheme Scheme(int id, params string[] debits)
        => new(id, "S" + id, debits.Select((d, i) => new AgnumSchemeRow(i + 1, "row" + i, d, "4430")).ToList());

    private static AccountingConfigService Service(WarehouseDbContext db, IEnumerable<AgnumScheme> schemes, params string[] expenses)
    {
        var agnum = new Mock<IAgnumReferenceClient>();
        agnum.Setup(a => a.GetSchemesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(schemes.ToList());
        var exp = new Mock<IExpenseAccountSource>();
        exp.Setup(e => e.GetExpenseAccountsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(expenses);
        return new AccountingConfigService(db, agnum.Object, exp.Object,
            Options.Create(new AccountingOptions { VatAccount = "24101" }));
    }

    private static StockAccountProfile Profile(string stock = "20110") => new() { Name = "Main", StockAccount = stock, AgnumSndId = 1, IsDefault = true };

    // ---- 4.6 validation ----

    [Fact]
    public void Validator_ReportsMissingStockVatAndExpenseRows()
    {
        var missing = PostingRuleValidator.FindMissingRows(
            Scheme(1, "99999"), Profile(), VatTreatment.Domestic, "24101", new[] { "6001" });
        missing.Should().HaveCount(3);
        missing.Should().Contain(m => m.Contains("20110")).And.Contain(m => m.Contains("24101")).And.Contain(m => m.Contains("6001"));
    }

    [Fact]
    public void Validator_NonDomestic_DoesNotRequireVatRow()
    {
        PostingRuleValidator.FindMissingRows(Scheme(1, "20110"), Profile(), VatTreatment.EuAcquisition, "24101", Array.Empty<string>())
            .Should().BeEmpty();
    }

    [Fact]
    public async Task SaveRule_SchemeWithoutRequiredRow_IsRejectedAndNothingStored()
    {
        await using var db = NewDb();
        var svc = Service(db, new[] { Scheme(7, "20110") }); // no VAT row
        var profile = await svc.SaveProfileAsync(Profile(), "acc", default);

        var act = () => svc.SaveRuleAsync(new AgnumPostingRule
        { StockAccountProfileId = profile.Id, VatTreatment = VatTreatment.Domestic, SchemeId = 7 }, "acc", default);

        var ex = await act.Should().ThrowAsync<AccountingConfigValidationException>();
        ex.Which.Problems.Should().ContainSingle(p => p.Contains("24101"));
        (await db.AgnumPostingRules.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task SaveRule_ValidScheme_IsStoredAndAudited()
    {
        await using var db = NewDb();
        var svc = Service(db, new[] { Scheme(7, "20110", "24101") });
        var profile = await svc.SaveProfileAsync(Profile(), "acc", default);

        await svc.SaveRuleAsync(new AgnumPostingRule
        { StockAccountProfileId = profile.Id, VatTreatment = VatTreatment.Domestic, SchemeId = 7, VatCode = "PVM1" }, "alice", default);
        await svc.SaveRuleAsync(new AgnumPostingRule
        { StockAccountProfileId = profile.Id, VatTreatment = VatTreatment.Domestic, SchemeId = 7, VatCode = "PVM2" }, "bob", default);

        (await db.AgnumPostingRules.SingleAsync()).VatCode.Should().Be("PVM2");
        var audit = await db.AccountingConfigAudit.Where(a => a.Entity == "AgnumPostingRule").OrderBy(a => a.Id).ToListAsync();
        audit.Select(a => (a.Action, a.ChangedBy)).Should().Equal(("Create", "alice"), ("Update", "bob"));
        audit[1].BeforeJson.Should().Contain("PVM1");
        audit[1].AfterJson.Should().Contain("PVM2");
    }

    [Fact]
    public async Task SaveProfile_ChangingStockAccount_RevalidatesExistingRules()
    {
        await using var db = NewDb();
        var svc = Service(db, new[] { Scheme(7, "20110", "24101") });
        var profile = await svc.SaveProfileAsync(Profile(), "acc", default);
        await svc.SaveRuleAsync(new AgnumPostingRule
        { StockAccountProfileId = profile.Id, VatTreatment = VatTreatment.Domestic, SchemeId = 7 }, "acc", default);

        var act = () => svc.SaveProfileAsync(new StockAccountProfile
        { Id = profile.Id, Name = "Main", StockAccount = "20120", AgnumSndId = 1, IsDefault = true }, "acc", default);

        await act.Should().ThrowAsync<AccountingConfigValidationException>();
    }

    [Fact]
    public async Task SaveRule_MissingExpenseAccountRow_IsRejected()
    {
        await using var db = NewDb();
        var svc = Service(db, new[] { Scheme(7, "20110", "24101") }, "6001");
        var profile = await svc.SaveProfileAsync(Profile(), "acc", default);

        var act = () => svc.SaveRuleAsync(new AgnumPostingRule
        { StockAccountProfileId = profile.Id, VatTreatment = VatTreatment.Domestic, SchemeId = 7 }, "acc", default);

        (await act.Should().ThrowAsync<AccountingConfigValidationException>()).Which.Problems
            .Should().Contain(p => p.Contains("6001"));
    }

    [Fact]
    public async Task SaveProfile_NewDefault_ClearsPreviousDefaultAndAudits()
    {
        await using var db = NewDb();
        var svc = Service(db, Array.Empty<AgnumScheme>());
        var a = await svc.SaveProfileAsync(Profile(), "acc", default);
        await svc.SaveProfileAsync(new StockAccountProfile { Name = "Second", StockAccount = "20120", IsDefault = true }, "acc", default);

        (await db.StockAccountProfiles.SingleAsync(p => p.Id == a.Id)).IsDefault.Should().BeFalse();
        (await db.StockAccountProfiles.CountAsync(p => p.IsDefault)).Should().Be(1);
    }

    // ---- 4.5 resolution ----

    [Fact]
    public void ResolveProfile_OwnThenParentThenDefault()
    {
        var cats = new Dictionary<int, (int? ParentId, int? ProfileId)>
        {
            [1] = (null, 5), [2] = (1, null), [3] = (2, 9), [4] = (null, null)
        };
        AccountingConfigService.ResolveProfileId(3, cats, 100).Should().Be(9);
        AccountingConfigService.ResolveProfileId(2, cats, 100).Should().Be(5);
        AccountingConfigService.ResolveProfileId(4, cats, 100).Should().Be(100);
    }

    // ---- 4.11 supplier sync ----

    private static AgnumSupplierSyncService Sync(WarehouseDbContext db, params AgnumClientDto[] clients)
    {
        var agnum = new Mock<IAgnumReferenceClient>();
        agnum.Setup(a => a.GetClientsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(clients);
        return new AgnumSupplierSyncService(db, agnum.Object, new NullLogger<AgnumSupplierSyncService>());
    }

    private static AgnumClientDto Client(int id, string code, string? krd = "Y", string? vat = null, string? country = null)
        => new() { Id = id, Code = code, Name = "N" + id, Krd = krd, VatCode = vat, Country = country, ClientRoles = new List<string> { "SUPPLIER" } };

    [Fact]
    public async Task Sync_NeverRelinksByCoincidingCode()
    {
        await using var db = NewDb();
        db.Suppliers.Add(new Supplier { Code = "ACME", Name = "Manual ACME", AgnumClientId = null });
        await db.SaveChangesAsync();

        var result = await Sync(db, Client(501, "ACME")).SyncAsync();

        result.CodeConflicts.Should().Be(1);
        var all = await db.Suppliers.OrderBy(s => s.Id).ToListAsync();
        all.Should().HaveCount(2);
        all[0].AgnumClientId.Should().BeNull();
        all[0].Name.Should().Be("Manual ACME");
        all[1].AgnumClientId.Should().Be(501);
    }

    [Fact]
    public async Task Sync_UpdatesByAgnumClientIdEvenWhenCodeChanged()
    {
        await using var db = NewDb();
        db.Suppliers.Add(new Supplier { Code = "OLD", Name = "Old", AgnumClientId = 7 });
        await db.SaveChangesAsync();

        await Sync(db, Client(7, "NEWCODE")).SyncAsync();

        var s = await db.Suppliers.SingleAsync();
        s.AgnumClientId.Should().Be(7);
        s.Name.Should().Be("N7");
        s.Code.Should().Be("OLD");
    }

    [Fact]
    public async Task Sync_SupplierAbsentFromResponse_IsFlaggedAndRefusedForPosting_AndRestoredWhenBack()
    {
        await using var db = NewDb();
        await Sync(db, Client(1, "A"), Client(2, "B")).SyncAsync();

        var r = await Sync(db, Client(1, "A")).SyncAsync();
        r.MarkedMissing.Should().Be(1);
        var b = await db.Suppliers.SingleAsync(s => s.AgnumClientId == 2);
        b.MissingInAgnum.Should().BeTrue();
        AgnumSupplierSyncService.CheckPostable(b).Allowed.Should().BeFalse();

        var r2 = await Sync(db, Client(1, "A"), Client(2, "B")).SyncAsync();
        r2.Restored.Should().Be(1);
        AgnumSupplierSyncService.CheckPostable(await db.Suppliers.SingleAsync(s => s.AgnumClientId == 2)).Allowed.Should().BeTrue();
    }

    [Fact]
    public async Task Sync_EmptyResponse_AbortsWithoutFlagging()
    {
        await using var db = NewDb();
        db.Suppliers.Add(new Supplier { Code = "A", Name = "A", AgnumClientId = 1 });
        await db.SaveChangesAsync();

        var act = () => Sync(db).SyncAsync();

        await act.Should().ThrowAsync<AgnumApiUnavailableException>();
        (await db.Suppliers.SingleAsync()).MissingInAgnum.Should().BeFalse();
    }

    [Fact]
    public void CheckPostable_RefusesUnlinkedMissingAndNonCreditor()
    {
        AgnumSupplierSyncService.CheckPostable(new Supplier { AgnumClientId = null }).Allowed.Should().BeFalse();
        AgnumSupplierSyncService.CheckPostable(new Supplier { AgnumClientId = 1, MissingInAgnum = true }).Allowed.Should().BeFalse();
        AgnumSupplierSyncService.CheckPostable(new Supplier { AgnumClientId = 1, IsAgnumCreditor = false }).Allowed.Should().BeFalse();
        AgnumSupplierSyncService.CheckPostable(new Supplier { AgnumClientId = 1, IsAgnumCreditor = true }).Allowed.Should().BeTrue();
    }

    [Theory]
    [InlineData("LT123456789", null, SupplierKind.Domestic)]
    [InlineData("DE811234567", null, SupplierKind.Eu)]
    [InlineData("GB123456789", null, SupplierKind.NonEu)]
    [InlineData(null, "LT", SupplierKind.Domestic)]
    [InlineData(null, "PL", SupplierKind.Eu)]
    [InlineData(null, "CN", SupplierKind.NonEu)]
    [InlineData(null, null, null)]
    public void ResolveKind_ByVatPrefixThenCountry(string? vat, string? country, SupplierKind? expected)
        => AgnumSupplierSyncService.ResolveKind(vat, country).Should().Be(expected);

    [Fact]
    public async Task Sync_ManualKindSurvivesNextSync()
    {
        await using var db = NewDb();
        await Sync(db, Client(1, "A", vat: "DE811234567")).SyncAsync();
        var s = await db.Suppliers.SingleAsync();
        s.Kind.Should().Be(SupplierKind.Eu);
        s.Kind = SupplierKind.NonEu;
        await db.SaveChangesAsync();

        await Sync(db, Client(1, "A", vat: "DE811234567")).SyncAsync();
        (await db.Suppliers.SingleAsync()).Kind.Should().Be(SupplierKind.NonEu);
    }

    // ---- 5.2 client against a fake handler ----

    private sealed class FakeHandler : HttpMessageHandler
    {
        public List<string> Requests { get; } = new();
        public Func<string, (HttpStatusCode, string)> Respond { get; set; } = _ => (HttpStatusCode.OK, "[]");

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request.RequestUri!.PathAndQuery);
            var (code, body) = Respond(request.RequestUri.AbsolutePath);
            return Task.FromResult(new HttpResponseMessage(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task ReferenceClient_ParsesSchemesAndHitsContractRoutes()
    {
        var handler = new FakeHandler
        {
            Respond = path => path switch
            {
                "/api/reference/schemes" => (HttpStatusCode.OK,
                    """[{"id_sch":12,"name":"Purchase","rows":[{"no":1,"name":"Stock","debit":"20110","credit":"4430"}]}]"""),
                "/api/reference/warehouses" => (HttpStatusCode.OK, """[{"id_snd":3,"code":"W","name":"Main"}]"""),
                "/api/reference/accounts" => (HttpStatusCode.OK, """[{"code":"20110","name":"Stock"}]"""),
                "/api/reference/vat-codes" => (HttpStatusCode.OK, """[{"pvm_kod":"PVM1"}]"""),
                _ => (HttpStatusCode.NotFound, "")
            }
        };
        var client = new AgnumReferenceClient(new HttpClient(handler) { BaseAddress = new Uri("http://agnum.test/") });

        var schemes = await client.GetSchemesAsync();
        schemes.Single().IdSch.Should().Be(12);
        schemes.Single().Rows!.Single().Debit.Should().Be("20110");
        (await client.GetWarehousesAsync()).Single().IdSnd.Should().Be(3);
        (await client.GetAccountsAsync()).Single().Code.Should().Be("20110");
        (await client.GetVatCodesAsync()).Single().Code.Should().Be("PVM1");
        handler.Requests.Should().Equal(
            "/api/reference/schemes", "/api/reference/warehouses", "/api/reference/accounts", "/api/reference/vat-codes");
    }

    [Fact]
    public async Task ReferenceClient_ServerError_ThrowsUnavailable()
    {
        var handler = new FakeHandler { Respond = _ => (HttpStatusCode.InternalServerError, "") };
        var client = new AgnumReferenceClient(new HttpClient(handler) { BaseAddress = new Uri("http://agnum.test/") });

        var act = () => client.GetSchemesAsync();

        await act.Should().ThrowAsync<AgnumApiUnavailableException>();
    }
}
