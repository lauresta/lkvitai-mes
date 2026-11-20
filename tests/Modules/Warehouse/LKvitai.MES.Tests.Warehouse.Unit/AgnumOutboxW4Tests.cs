using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Blueprint stage W4: outbox, sender, un-post, reconciliation, against a fake Agnum service.</summary>
public sealed class AgnumOutboxW4Tests
{
    private static readonly DateOnly Date = new(2026, 11, 20);

    // ---- fakes ----

    private sealed class TestTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 20, 10, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    /// <summary>Behaves like the purchase posting routes of agnum-api-deploy (idempotency by key, revision and hash).</summary>
    private sealed class FakeAgnum : HttpMessageHandler
    {
        private readonly object _lock = new();
        public Dictionary<string, (int Revision, string Hash, string Body, long IdDok)> Documents { get; } = new();
        public int PostCalls { get; private set; }
        public int DeleteCalls { get; private set; }
        public int GetCalls { get; private set; }
        /// <summary>Commit the document, then fail the call like a client timeout does.</summary>
        public int TimeoutAfterCommit { get; set; }
        public int ServerErrors { get; set; }
        public HttpStatusCode? PostAnswer { get; set; }
        public HttpStatusCode? DeleteAnswer { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = request.RequestUri!.AbsolutePath;
            if (request.Method == HttpMethod.Get && path.StartsWith("/api/purchase-postings/", StringComparison.Ordinal))
            {
                var key = path[^36..];
                lock (_lock)
                {
                    GetCalls++;
                    return Task.FromResult(Documents.TryGetValue(key, out var d)
                        ? Json(HttpStatusCode.OK, $$"""{"found":true,"from_mes":true,"key":"{{key}}","revision":{{d.Revision}},"hash":"{{d.Hash}}","id_dok":{{d.IdDok}},"id_op":{{d.IdDok + 1}},"postings":[{"debit":"20110","credit":"4431","amount":100.00,"amount_eur":100.00}]}""")
                        : Json(HttpStatusCode.OK, $$"""{"found":false,"key":"{{key}}"}"""));
                }
            }

            if (request.Method == HttpMethod.Post && path == "/api/purchase-postings")
            {
                var body = request.Content!.ReadAsStringAsync(ct).GetAwaiter().GetResult();
                using var doc = JsonDocument.Parse(body);
                var key = doc.RootElement.GetProperty("key").GetString()!;
                var revision = doc.RootElement.GetProperty("revision").GetInt32();
                var hash = doc.RootElement.GetProperty("payload_hash").GetString()!;
                lock (_lock)
                {
                    PostCalls++;
                    if (ServerErrors > 0) { ServerErrors--; return Task.FromResult(Json(HttpStatusCode.InternalServerError, """{"message":"boom"}""")); }
                    if (PostAnswer is { } forced) return Task.FromResult(Json(forced, """{"message":"rejected"}"""));
                    if (Documents.TryGetValue(key, out var existing))
                    {
                        return Task.FromResult(existing.Revision == revision && existing.Hash == hash
                            ? Json(HttpStatusCode.OK, $$"""{"id_dok":{{existing.IdDok}},"id_op":{{existing.IdDok + 1}},"postings":[]}""")
                            : Json(HttpStatusCode.Conflict, $$"""{"message":"other content","id_dok":{{existing.IdDok}}}"""));
                    }

                    var id = 1000 + Documents.Count;
                    Documents[key] = (revision, hash, body, id);
                    if (TimeoutAfterCommit > 0) { TimeoutAfterCommit--; throw new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout."); }
                    return Task.FromResult(Json(HttpStatusCode.Created, $$"""{"id_dok":{{id}},"id_op":{{id + 1}},"postings":[{"debit":"20110","credit":"4431","amount":100.00,"amount_eur":100.00}]}"""));
                }
            }

            if (request.Method == HttpMethod.Delete)
            {
                lock (_lock)
                {
                    DeleteCalls++;
                    if (DeleteAnswer is { } forced) return Task.FromResult(Json(forced, """{"message":"paid"}"""));
                    var key = path[^36..];
                    return Task.FromResult(Documents.Remove(key)
                        ? Json(HttpStatusCode.OK, """{"deleted":true}""")
                        : Json(HttpStatusCode.NotFound, """{"message":"unknown key"}"""));
                }
            }

            return Task.FromResult(Json(HttpStatusCode.NotFound, "{}"));
        }

        private static HttpResponseMessage Json(HttpStatusCode code, string body)
            => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }

    private sealed class FakeReverser : IPostingReverser
    {
        public List<(string Type, int Id)> Calls { get; } = new();
        public Task ReverseAsync(string documentType, int documentId, string user, CancellationToken ct)
        {
            Calls.Add((documentType, documentId));
            return Task.CompletedTask;
        }
    }

    private sealed class GateInterceptor : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Armed { get; set; }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Armed)
            {
                Armed = false;
                Entered.TrySetResult();
                await Gate.Task;
            }

            return result;
        }
    }

    private static DbContextOptions<WarehouseDbContext> Options(string name, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<WarehouseDbContext>().UseInMemoryDatabase(name);
        if (interceptors.Length > 0) builder.AddInterceptors(interceptors);
        return builder.Options;
    }

    private static AgnumOutboxSender Sender(WarehouseDbContext db, FakeAgnum agnum, IPostingReverser reverser, TimeProvider time)
        => new(db, new AgnumPostingClient(new HttpClient(agnum) { BaseAddress = new Uri("http://agnum.test/") }),
            reverser, new NullLogger<AgnumOutboxSender>(), time);

    private static async Task<WarehouseDbContext> SeedAsync(string name, bool enabled = true)
    {
        var db = new WarehouseDbContext(Options(name));
        db.AgnumSendingSettings.Add(new AgnumSendingSettings { Enabled = enabled, StartDate = new DateOnly(2026, 1, 1) });
        await db.SaveChangesAsync();
        return db;
    }

    private static AgnumDocumentOutbox Row(int documentId = 1, int supplierId = 1, int revision = 1, Guid? key = null, DateOnly? date = null, string number = "INV 1")
    {
        var payload = AgnumPayloadBuilder.Build(new PostingPayloadInput(
            key ?? Guid.NewGuid(), revision, number, date ?? Date, 62, 493, 26, "EUR", 1m, 100m, 21m, "PVM1", "20110", []));
        return new AgnumDocumentOutbox
        {
            Key = Guid.Parse(payload.Request.Key), Revision = revision, DocumentType = OutboxDocumentType.Shipment, DocumentId = documentId,
            SupplierId = supplierId, DocumentNumber = number, DocumentDate = date ?? Date, Currency = "EUR", GrossAmount = 121m,
            PayloadJson = payload.Json, PayloadHash = payload.Hash, Status = OutboxStatus.Pending, NextAttemptAt = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero),
            CreatedAt = new DateTimeOffset(2026, 11, 20, 9, 0, 0, TimeSpan.Zero), CreatedBy = "test"
        };
    }

    // ---- contract ----

    [Fact]
    public void Payload_UsesTheFieldNamesOfTheServiceContract_AndEurFromTheDocumentRate()
    {
        var built = AgnumPayloadBuilder.Build(new PostingPayloadInput(
            Guid.Parse("11111111-2222-3333-4444-555555555555"), 1, "FS 461/MGWG/10/2026", new DateOnly(2026, 10, 29), 62, 493, 26,
            "PLN", 4.39385m, 34428.98m, 0m, "", "20110", [("6001", 100m)]));

        using var doc = JsonDocument.Parse(built.Json);
        var root = doc.RootElement;
        root.GetProperty("key").GetString().Should().Be("11111111-2222-3333-4444-555555555555");
        root.GetProperty("kind").GetString().Should().Be("invoice");
        root.GetProperty("doc_no").GetString().Should().Be("FS 461/MGWG/10/2026");
        root.GetProperty("doc_date").GetString().Should().Be("2026-10-29");
        root.GetProperty("client_id").GetInt64().Should().Be(62);
        root.GetProperty("snd_id").GetInt64().Should().Be(493);
        root.GetProperty("scheme_id").GetInt64().Should().Be(26);
        root.GetProperty("currency").GetString().Should().Be("PLN");
        root.GetProperty("rate_eur_per_unit").GetDecimal().Should().Be(0.22759084m);
        root.GetProperty("gross").GetDecimal().Should().Be(34428.98m);
        root.GetProperty("payload_hash").GetString().Should().Be(built.Hash).And.HaveLength(64);

        var postings = root.GetProperty("postings").EnumerateArray().ToList();
        postings.Select(p => p.GetProperty("debit_account").GetString()).Should().Equal("20110", "6001");
        postings.Select(p => p.GetProperty("amount").GetDecimal()).Should().Equal(34328.98m, 100m);
        // eur = Round(amount x rateEurPerUnit, 2): the service recomputes it for every posting and answers 422 otherwise.
        foreach (var p in built.Request.Postings)
            p.AmountEur.Should().Be(decimal.Round(p.Amount * built.Request.RateEurPerUnit, 2, MidpointRounding.AwayFromZero));
        built.Request.Postings.Sum(p => p.Amount).Should().Be(built.Request.Net);
    }

    [Fact]
    public void Payload_IsDeterministic_AndTheHashChangesWithTheContent()
    {
        PostingPayloadInput Input(decimal net) => new(Guid.Empty, 1, "A", Date, 1, 2, 3, "EUR", 1m, net, 0m, null, "20110", []);
        AgnumPayloadBuilder.Build(Input(10m)).Hash.Should().Be(AgnumPayloadBuilder.Build(Input(10m)).Hash);
        AgnumPayloadBuilder.Build(Input(10m)).Hash.Should().NotBe(AgnumPayloadBuilder.Build(Input(10.01m)).Hash);
    }

    // ---- sender ----

    [Fact]
    public async Task Sender_SendsAPendingRow_AndStoresTheAnswer()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        db.AgnumOutbox.Add(Row());
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum();

        var run = await Sender(db, agnum, new FakeReverser(), new TestTime()).RunOnceAsync(default);

        run.Sent.Should().Be(1);
        var row = await db.AgnumOutbox.SingleAsync();
        row.Status.Should().Be(OutboxStatus.Sent);
        row.AgnumDocumentId.Should().Be(1000);
        row.AppliedPostingsJson.Should().Contain("20110");
        agnum.GetCalls.Should().Be(1, "GET always precedes POST");
        agnum.PostCalls.Should().Be(1);
    }

    [Fact]
    public async Task Sender_TimeoutAfterCommit_DoesNotCreateASecondDocument()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        db.AgnumOutbox.Add(Row());
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum { TimeoutAfterCommit = 1 };
        var time = new TestTime();
        var sender = Sender(db, agnum, new FakeReverser(), time);

        await sender.RunOnceAsync(default);
        var afterTimeout = await db.AgnumOutbox.AsNoTracking().SingleAsync();
        afterTimeout.Status.Should().Be(OutboxStatus.Unknown);
        agnum.Documents.Should().HaveCount(1, "the service committed before the timeout");

        time.Now += TimeSpan.FromMinutes(2);
        await sender.RunOnceAsync(default);

        (await db.AgnumOutbox.AsNoTracking().SingleAsync()).Status.Should().Be(OutboxStatus.Sent);
        agnum.Documents.Should().HaveCount(1);
        agnum.PostCalls.Should().Be(1, "the second attempt found the document by GET and did not POST again");
    }

    [Fact]
    public async Task Sender_TwoInstancesRacingForOneRow_SendItOnce()
    {
        var name = Guid.NewGuid().ToString();
        await using (var seed = await SeedAsync(name))
        {
            seed.AgnumOutbox.Add(Row());
            await seed.SaveChangesAsync();
        }

        var gate = new GateInterceptor();
        await using var dbA = new WarehouseDbContext(Options(name));
        await using var dbB = new WarehouseDbContext(Options(name, gate));
        var agnum = new FakeAgnum();
        var reverser = new FakeReverser();
        var time = new TestTime();

        // B has read the row as Pending and is about to claim it when A runs to the end.
        gate.Armed = true;
        var runB = Task.Run(() => Sender(dbB, agnum, reverser, time).RunOnceAsync(default));
        await gate.Entered.Task;
        var a = await Sender(dbA, agnum, reverser, time).RunOnceAsync(default);
        gate.Gate.SetResult();
        var b = await runB;

        a.Sent.Should().Be(1);
        b.Sent.Should().Be(0);
        b.Skipped.Should().Be(1, "the claim of B lost against the version of A");
        agnum.PostCalls.Should().Be(1);
        agnum.Documents.Should().HaveCount(1);
    }

    [Fact]
    public async Task Sender_TwoInstancesRunningTogether_NeverPostTwice()
    {
        var name = Guid.NewGuid().ToString();
        await using (var seed = await SeedAsync(name))
        {
            for (var i = 1; i <= 5; i++) seed.AgnumOutbox.Add(Row(documentId: i, supplierId: i));
            await seed.SaveChangesAsync();
        }

        var agnum = new FakeAgnum();
        var time = new TestTime();
        await using var dbA = new WarehouseDbContext(Options(name));
        await using var dbB = new WarehouseDbContext(Options(name));
        await Task.WhenAll(
            Task.Run(() => Sender(dbA, agnum, new FakeReverser(), time).RunOnceAsync(default)),
            Task.Run(() => Sender(dbB, agnum, new FakeReverser(), time).RunOnceAsync(default)));

        agnum.Documents.Should().HaveCount(5);
        agnum.PostCalls.Should().Be(5);
    }

    [Fact]
    public async Task Sender_RowInSending_IsNotTakenAgainUntilItsLeaseExpires()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var row = Row();
        row.Status = OutboxStatus.Sending;
        row.LeasedUntil = new DateTimeOffset(2026, 11, 20, 10, 5, 0, TimeSpan.Zero);
        db.AgnumOutbox.Add(row);
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum();
        var time = new TestTime();
        var sender = Sender(db, agnum, new FakeReverser(), time);

        await sender.RunOnceAsync(default);
        agnum.GetCalls.Should().Be(0);

        time.Now += TimeSpan.FromMinutes(6);
        await sender.RunOnceAsync(default);
        (await db.AgnumOutbox.AsNoTracking().SingleAsync()).Status.Should().Be(OutboxStatus.Sent);
    }

    [Fact]
    public async Task Sender_ServerErrors_BackOff1_5_15_60Minutes_ThenNeedsReview()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        db.AgnumOutbox.Add(Row());
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum { ServerErrors = 99 };
        var time = new TestTime();
        var sender = Sender(db, agnum, new FakeReverser(), time);

        foreach (var minutes in new[] { 1, 5, 15, 60 })
        {
            var before = time.Now;
            await sender.RunOnceAsync(default);
            var row = await db.AgnumOutbox.AsNoTracking().SingleAsync();
            row.Status.Should().Be(OutboxStatus.Pending);
            (row.NextAttemptAt - before).Should().Be(TimeSpan.FromMinutes(minutes));
            time.Now = row.NextAttemptAt;
        }

        await sender.RunOnceAsync(default);
        var last = await db.AgnumOutbox.AsNoTracking().SingleAsync();
        last.Status.Should().Be(OutboxStatus.NeedsReview);
        last.LastError.Should().Contain("500");

        var posts = agnum.PostCalls;
        time.Now += TimeSpan.FromHours(3);
        await sender.RunOnceAsync(default);
        agnum.PostCalls.Should().Be(posts, "NeedsReview is not retried automatically");
    }

    [Theory]
    [InlineData(HttpStatusCode.Conflict)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task Sender_ConflictOrRejection_GoesToNeedsReviewWithoutRetry(HttpStatusCode answer)
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        db.AgnumOutbox.Add(Row());
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum { PostAnswer = answer };
        var time = new TestTime();
        var sender = Sender(db, agnum, new FakeReverser(), time);

        await sender.RunOnceAsync(default);
        time.Now += TimeSpan.FromHours(2);
        await sender.RunOnceAsync(default);

        (await db.AgnumOutbox.AsNoTracking().SingleAsync()).Status.Should().Be(OutboxStatus.NeedsReview);
        agnum.PostCalls.Should().Be(1);
    }

    [Fact]
    public async Task Sender_OtherContentUnderTheSameKey_NeedsReview_AndNothingIsPosted()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var row = Row();
        db.AgnumOutbox.Add(row);
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum();
        agnum.Documents[row.Key.ToString("D")] = (1, new string('a', 64), "{}", 7);

        await Sender(db, agnum, new FakeReverser(), new TestTime()).RunOnceAsync(default);

        (await db.AgnumOutbox.AsNoTracking().SingleAsync()).Status.Should().Be(OutboxStatus.NeedsReview);
        agnum.PostCalls.Should().Be(0);
    }

    [Fact]
    public async Task Sender_KeepsTheOrderPerSupplier_AndDoesNotSendWhenDisabledOrBeforeTheStartDate()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var first = Row(documentId: 1, supplierId: 7);
        first.Status = OutboxStatus.NeedsReview;
        var second = Row(documentId: 2, supplierId: 7);
        var otherSupplier = Row(documentId: 3, supplierId: 8);
        var old = Row(documentId: 4, supplierId: 9, date: new DateOnly(2025, 12, 31));
        db.AgnumOutbox.AddRange(first, second, otherSupplier, old);
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum();

        await Sender(db, agnum, new FakeReverser(), new TestTime()).RunOnceAsync(default);

        agnum.Documents.Should().ContainKey(otherSupplier.Key.ToString("D")).And.HaveCount(1);
        (await db.AgnumOutbox.AsNoTracking().SingleAsync(r => r.DocumentId == 2)).Status.Should().Be(OutboxStatus.Pending);
        (await db.AgnumOutbox.AsNoTracking().SingleAsync(r => r.DocumentId == 4)).Status.Should().Be(OutboxStatus.Pending);

        var settings = await db.AgnumSendingSettings.SingleAsync();
        settings.Enabled = false;
        await db.SaveChangesAsync();
        var posts = agnum.PostCalls;
        await Sender(db, agnum, new FakeReverser(), new TestTime()).RunOnceAsync(default);
        agnum.PostCalls.Should().Be(posts);
    }

    // ---- annulment ----

    private static AgnumDocumentOutbox AnnulOf(AgnumDocumentOutbox post) => new()
    {
        Key = post.Key, Revision = post.Revision, DocumentType = post.DocumentType, DocumentId = post.DocumentId, SupplierId = post.SupplierId,
        DocumentNumber = post.DocumentNumber, DocumentDate = post.DocumentDate, Currency = post.Currency, GrossAmount = post.GrossAmount,
        Operation = OutboxOperation.Annul, PayloadJson = "{}", PayloadHash = post.PayloadHash, Status = OutboxStatus.Pending,
        NextAttemptAt = post.NextAttemptAt, CreatedAt = post.CreatedAt.AddMinutes(1), CreatedBy = "test"
    };

    [Fact]
    public async Task Annul_DeletesTheDocument_ReversesLocally_AndClosesBothRows()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var post = Row();
        post.Status = OutboxStatus.Sent;
        db.AgnumOutbox.Add(post);
        await db.SaveChangesAsync();
        db.AgnumOutbox.Add(AnnulOf(post));
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum();
        agnum.Documents[post.Key.ToString("D")] = (1, post.PayloadHash, post.PayloadJson, 5);
        var reverser = new FakeReverser();

        await Sender(db, agnum, reverser, new TestTime()).RunOnceAsync(default);

        agnum.DeleteCalls.Should().Be(1);
        agnum.Documents.Should().BeEmpty();
        reverser.Calls.Should().Equal((OutboxDocumentType.Shipment, 1));
        (await db.AgnumOutbox.AsNoTracking().SingleAsync(r => r.Operation == OutboxOperation.Annul)).Status.Should().Be(OutboxStatus.Sent);
        (await db.AgnumOutbox.AsNoTracking().SingleAsync(r => r.Operation == OutboxOperation.Post)).Status.Should().Be(OutboxStatus.Annulled);
    }

    [Fact]
    public async Task Annul_WhenAgnumNeverGotTheDocument_ReversesWithoutDelete()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var post = Row();
        post.Status = OutboxStatus.Annulled;
        db.AgnumOutbox.Add(post);
        await db.SaveChangesAsync();
        db.AgnumOutbox.Add(AnnulOf(post));
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum();
        var reverser = new FakeReverser();

        await Sender(db, agnum, reverser, new TestTime()).RunOnceAsync(default);

        agnum.DeleteCalls.Should().Be(0);
        reverser.Calls.Should().HaveCount(1);
    }

    [Fact]
    public async Task Annul_OfAPaidDocument_NeedsReview_AndDoesNotReverseLocally()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var post = Row();
        post.Status = OutboxStatus.Sent;
        db.AgnumOutbox.Add(post);
        await db.SaveChangesAsync();
        db.AgnumOutbox.Add(AnnulOf(post));
        await db.SaveChangesAsync();
        var agnum = new FakeAgnum { DeleteAnswer = HttpStatusCode.Conflict };
        agnum.Documents[post.Key.ToString("D")] = (1, post.PayloadHash, post.PayloadJson, 5);
        var reverser = new FakeReverser();

        await Sender(db, agnum, reverser, new TestTime()).RunOnceAsync(default);

        (await db.AgnumOutbox.AsNoTracking().SingleAsync(r => r.Operation == OutboxOperation.Annul)).Status.Should().Be(OutboxStatus.NeedsReview);
        reverser.Calls.Should().BeEmpty();
        agnum.Documents.Should().HaveCount(1);
    }

    // ---- queue operations ----

    private static AgnumOutboxService Queue(WarehouseDbContext db) => new(db, new TestTime());

    [Fact]
    public async Task PlanUnpost_UnsentPostingIsReversedAtOnce_SentPostingGetsOneAnnulRow()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var unsent = Row(documentId: 1);
        var sent = Row(documentId: 2);
        sent.Status = OutboxStatus.Sent;
        db.AgnumOutbox.AddRange(unsent, sent);
        await db.SaveChangesAsync();
        var queue = Queue(db);

        (await queue.PlanUnpostAsync(OutboxDocumentType.Shipment, 1, 1, "Auto", unsent.Key, 1, "X", Date, "EUR", 121m, "u", default))
            .Should().Be(UnpostPlan.ReverseNow);
        (await queue.PlanUnpostAsync(OutboxDocumentType.Shipment, 2, 1, "Auto", sent.Key, 1, "X", Date, "EUR", 121m, "u", default))
            .Should().Be(UnpostPlan.AnnulQueued);
        (await queue.PlanUnpostAsync(OutboxDocumentType.Shipment, 2, 1, "Auto", sent.Key, 1, "X", Date, "EUR", 121m, "u", default))
            .Should().Be(UnpostPlan.AlreadyQueued);
        (await queue.PlanUnpostAsync(OutboxDocumentType.Shipment, 3, 1, "ManualInAgnum", null, 1, "X", Date, "EUR", 121m, "u", default))
            .Should().Be(UnpostPlan.ReverseNow);
        db.AgnumOutbox.Count(r => r.Operation == OutboxOperation.Annul).Should().Be(1);
    }

    [Fact]
    public async Task PlanUnpost_AfterATimeout_SupersedesThePostingAndLetsTheAnnulLookItUpFirst()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var unknown = Row();
        unknown.Status = OutboxStatus.Unknown;
        unknown.Attempts = 1;
        db.AgnumOutbox.Add(unknown);
        await db.SaveChangesAsync();

        (await Queue(db).PlanUnpostAsync(OutboxDocumentType.Shipment, 1, 1, "Auto", unknown.Key, 1, "X", Date, "EUR", 121m, "u", default))
            .Should().Be(UnpostPlan.AnnulQueued);

        var agnum = new FakeAgnum();
        agnum.Documents[unknown.Key.ToString("D")] = (1, unknown.PayloadHash, unknown.PayloadJson, 5); // it did reach Agnum
        await Sender(db, agnum, new FakeReverser(), new TestTime()).RunOnceAsync(default);

        agnum.PostCalls.Should().Be(0);
        agnum.Documents.Should().BeEmpty("the annulment found the document by GET and deleted it");
    }

    [Fact]
    public async Task RetryAndMarkManual_ChangeTheRowAndTheDocument()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        db.InboundShipments.Add(new InboundShipment { Id = 1, ReferenceNumber = "IN-1", SupplierId = 1, PostingStatus = "Posted" });
        var failed = Row(documentId: 1);
        failed.Status = OutboxStatus.NeedsReview;
        failed.Attempts = 5;
        db.AgnumOutbox.Add(failed);
        await db.SaveChangesAsync();
        var queue = Queue(db);

        await queue.RetryAsync(failed.Id, default);
        var retried = await db.AgnumOutbox.AsNoTracking().SingleAsync();
        retried.Status.Should().Be(OutboxStatus.Pending);
        retried.Attempts.Should().Be(0);

        await queue.Invoking(q => q.MarkManualAsync(failed.Id, " ", "acc", default)).Should().ThrowAsync<OutboxActionException>();
        await queue.MarkManualAsync(failed.Id, "multi-rate VAT", "acc", default);
        (await db.AgnumOutbox.AsNoTracking().SingleAsync()).Status.Should().Be(OutboxStatus.Annulled);
        var shipment = await db.InboundShipments.AsNoTracking().SingleAsync();
        shipment.AccountingRoute.Should().Be("ManualInAgnum");
        shipment.ManualReason.Should().Be("multi-rate VAT");
    }

    [Fact]
    public async Task Settings_RequireAStartDateToBeSwitchedOn_AndAreJournaled()
    {
        await using var db = new WarehouseDbContext(Options(Guid.NewGuid().ToString()));
        var queue = Queue(db);
        await queue.Invoking(q => q.SaveSettingsAsync(true, null, "acc", default)).Should().ThrowAsync<OutboxActionException>();
        await queue.SaveSettingsAsync(true, new DateOnly(2026, 12, 1), "acc", default);
        var dto = await queue.GetSettingsDtoAsync("http://agnum.test/", default);
        dto.Enabled.Should().BeTrue();
        dto.StartDate.Should().Be(new DateOnly(2026, 12, 1));
        db.AccountingConfigAudit.Should().ContainSingle(a => a.Entity == "AgnumSendingSettings");
    }

    private static PurchasePostingInput PostingInput(InboundShipment shipment) => new()
    {
        Shipment = shipment, Supplier = new Supplier { AgnumClientId = 5, Kind = SupplierKind.Domestic },
        CostTypes = new Dictionary<string, AdditionalCostType>(), ItemProfiles = new Dictionary<int, int?> { [10] = 1 },
        HasPostingRule = (_, _) => true, Rate = new PostingRate(FxRateStatus.Ok, 1m, Date, "EUR", null)
    };

    [Fact]
    public void ManualInAgnum_NeedsAReason()
    {
        var shipment = new InboundShipment
        {
            Id = 1, SupplierId = 1, InvoiceNumber = "A", InvoiceDate = Date, NetAmount = 10m, VatAmount = 0m, AccountingRoute = "ManualInAgnum",
            Lines = { new InboundShipmentLine { Id = 1, ItemId = 10, Item = new Item { Id = 10, InternalSKU = "S", ItemType = ItemType.Stock }, ExpectedQty = 1m, UnitPrice = 10m } }
        };
        PurchasePostingValidator.Validate(PostingInput(shipment)).Should().Contain(p => p.Code == "ManualReasonRequired");
        shipment.ManualReason = "several VAT rates";
        PurchasePostingValidator.Validate(PostingInput(shipment)).Should().BeEmpty();
    }

    // ---- un-post ----

    private static InboundShipment PostedShipment(out List<StockValueRequest> requests)
    {
        var item = new Item { Id = 10, InternalSKU = "SKU", ItemType = ItemType.Stock };
        var shipment = new InboundShipment
        {
            Id = 1, ReferenceNumber = "IN-1", SupplierId = 1, InvoiceNumber = "INV 1", InvoiceDate = Date, Currency = "EUR",
            NetAmount = 1200m, VatAmount = 252m,
            Lines = { new InboundShipmentLine { Id = 1, ItemId = 10, Item = item, ExpectedQty = 100m, ReceivedQty = 100m, UnitPrice = 12m, BookedValueEur = 1080m } }
        };
        var snapshot = PostingSnapshot.Capture(shipment);
        requests = PurchaseDocumentService.Apply(shipment, PostingInput(shipment), "acc", 1);
        snapshot.Values = requests.Select(r => new SnapshotValue(r.Sku, r.Qty, r.ValueEur ?? 0m)).ToList();
        shipment.PostingSnapshotJson = snapshot.ToJson();
        return shipment;
    }

    private static void ApplyAll(ItemValuation stock, IEnumerable<StockValueRequest> requests)
    {
        foreach (var r in requests)
            foreach (var e in StockValueWriter.BuildMany(stock, 10, r))
                stock.Apply(e);
    }

    private static IEnumerable<StockValueRequest> Reversal(PostingSnapshot snapshot)
        => snapshot.Values.Select(v => StockValueRequest.ForTrueUp(v.Sku, v.Qty, -v.DiffEur, new StockValueDocument("InboundShipment", "1", "IN-1", "Un-post")));

    [Fact]
    public void Unpost_RestoresTheDocumentAndTheStockValueBeforePosting()
    {
        var doc = new StockValueDocument("InboundShipment", "1", "IN-1", "test");
        var stock = new ItemValuation();
        stock.Apply(stock.Inflow(10, StockValueKinds.Receipt, 100m, 1080m, doc));
        var shipment = PostedShipment(out var requests);
        ApplyAll(stock, requests);
        stock.ValueEur.Should().Be(1200m);
        shipment.PostingStatus.Should().Be("Posted");
        shipment.Lines.Single().FinalValueEur.Should().Be(1200m);

        var snapshot = PostingSnapshot.FromJson(shipment.PostingSnapshotJson)!;
        snapshot.ReceiptChangesSincePosting(shipment.Lines).Should().BeEmpty();
        snapshot.Restore(shipment);
        ApplyAll(stock, Reversal(snapshot));

        stock.ValueEur.Should().Be(1080m, "the value is back to the preliminary value of the receipts");
        stock.ValuedQty.Should().Be(100m);
        shipment.PostingStatus.Should().Be("NotPosted");
        shipment.PostedAt.Should().BeNull();
        shipment.PostingRevision.Should().Be(2, "posting and un-posting each count");
        shipment.NetEur.Should().BeNull();
        shipment.RateUnitsPerEur.Should().BeNull();
        var line = shipment.Lines.Single();
        line.FinalValueEur.Should().BeNull();
        line.BookedValueEur.Should().Be(1080m);
        shipment.PostingSnapshotJson.Should().BeNull();
    }

    [Fact]
    public void Unpost_AfterPartOfTheGoodsWasIssued_StillRestoresTheValueThatWasThere()
    {
        var doc = new StockValueDocument("InboundShipment", "1", "IN-1", "test");
        var stock = new ItemValuation();
        stock.Apply(stock.Inflow(10, StockValueKinds.Receipt, 100m, 1080m, doc));
        stock.Apply(stock.Outflow(10, StockValueKinds.Issue, 80m, doc)!);
        var before = stock.ValueEur;
        before.Should().Be(216m);

        var shipment = PostedShipment(out var requests);
        ApplyAll(stock, requests);
        stock.ValueEur.Should().Be(240m, "only the share still on stock changes the ledger");

        var snapshot = PostingSnapshot.FromJson(shipment.PostingSnapshotJson)!;
        snapshot.Restore(shipment);
        ApplyAll(stock, Reversal(snapshot));

        stock.ValueEur.Should().Be(before);
    }

    [Fact]
    public void Unpost_IsRefused_WhenGoodsWereReceivedAfterPosting()
    {
        var shipment = PostedShipment(out _);
        shipment.Lines.Single().ReceivedQty = 120m;
        PostingSnapshot.FromJson(shipment.PostingSnapshotJson)!.ReceiptChangesSincePosting(shipment.Lines).Should().NotBeEmpty();
    }

    // ---- reconciliation ----

    private sealed class PeriodHandler(string body) : HttpMessageHandler
    {
        public string? Uri { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Uri = request.RequestUri!.PathAndQuery;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
        }
    }

    [Fact]
    public async Task Reconciliation_ReportsMissingMismatchedUnexpectedForeignAndManualDocuments()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        db.Suppliers.Add(new Supplier { Id = 1, Code = "S1", Name = "S1", AgnumClientId = 62 });
        db.InboundShipments.Add(new InboundShipment
        {
            Id = 9, ReferenceNumber = "IN-9", SupplierId = 1, PostingStatus = "Posted", AccountingRoute = "ManualInAgnum",
            InvoiceNumber = "M 1", InvoiceDate = Date, ManualReason = "multi VAT"
        });
        var ok = Row(documentId: 1, number: "OK"); ok.Status = OutboxStatus.Sent;
        var missing = Row(documentId: 2, number: "MISSING"); missing.Status = OutboxStatus.Sent;
        var changed = Row(documentId: 3, number: "CHANGED"); changed.Status = OutboxStatus.Sent;
        var inFlight = Row(documentId: 4, number: "FLIGHT"); inFlight.Status = OutboxStatus.Unknown;
        db.AgnumOutbox.AddRange(ok, missing, changed, inFlight);
        await db.SaveChangesAsync();

        AgnumPostedDocument Found(AgnumDocumentOutbox r, decimal gross) => new()
        {
            FromMes = true, Key = r.Key.ToString("D"), Revision = r.Revision, Hash = r.PayloadHash, IdDok = 1, DocNo = r.DocumentNumber, ClientId = 62,
            Net = 100m, Vat = 21m, Gross = gross, Postings = [new AgnumResultPosting("20110", "4431", 100m, 100m)]
        };
        var period = new AgnumPeriod(
            [Found(ok, 121m), Found(changed, 130m), Found(inFlight, 121m),
                new AgnumPostedDocument { FromMes = true, Key = Guid.NewGuid().ToString("D"), Revision = 1, DocNo = "STRAY", ClientId = 62 }],
            [new AgnumPostedDocument { DocNo = "m1", ClientId = 62, Gross = 5m, IdDok = 77 },
             new AgnumPostedDocument { DocNo = "HAND", ClientId = 63, Gross = 9m, IdDok = 78 }]);

        var findings = await new AgnumOutboxReconciler(db, new AgnumPostingClient(new HttpClient(new FakeAgnum())), new TestTime())
            .CompareAsync(period, new DateOnly(2026, 11, 1), Date, DateTimeOffset.UtcNow, default);

        findings.Select(f => (f.Kind, f.DocumentNumber)).Should().BeEquivalentTo(new[]
        {
            (ReconciliationKinds.MissingInAgnum, (string?)"MISSING"),
            (ReconciliationKinds.Mismatch, (string?)"CHANGED"),
            (ReconciliationKinds.UnexpectedInAgnum, (string?)"STRAY"),
            (ReconciliationKinds.ForeignDocument, (string?)"HAND"),
        }, "the manual document is matched by supplier and normalized number; the in-flight row is not unexpected");
        findings.Single(f => f.Kind == ReconciliationKinds.Mismatch).Details.Should().Contain("gross 130");
    }

    [Fact]
    public async Task Reconciliation_Run_ReplacesTheFindingsAndStoresTheTime()
    {
        await using var db = await SeedAsync(Guid.NewGuid().ToString());
        var sent = Row(); sent.Status = OutboxStatus.Sent;
        db.AgnumOutbox.Add(sent);
        db.AgnumReconciliationFindings.Add(new AgnumReconciliationFinding { Kind = "Old", Details = "old" });
        await db.SaveChangesAsync();
        var handler = new PeriodHandler("""{"postings":[],"foreign_documents":[]}""");

        var count = await new AgnumOutboxReconciler(db, new AgnumPostingClient(new HttpClient(handler) { BaseAddress = new Uri("http://agnum.test/") }), new TestTime())
            .RunAsync(30, default);

        count.Should().Be(1);
        db.AgnumReconciliationFindings.Single().Kind.Should().Be(ReconciliationKinds.MissingInAgnum);
        handler.Uri.Should().Be("/api/purchase-postings?from=2026-10-22&to=2026-11-20");
        (await db.AgnumSendingSettings.SingleAsync()).LastReconciledAt.Should().NotBeNull();
    }
}
