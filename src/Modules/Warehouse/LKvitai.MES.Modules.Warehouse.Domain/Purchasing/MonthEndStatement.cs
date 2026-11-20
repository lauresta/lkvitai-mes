using LKvitai.MES.Contracts.Events;

namespace LKvitai.MES.Modules.Warehouse.Domain.Purchasing;

/// <summary>One StockValueChanged event reduced to what the month-end reports need.</summary>
public sealed record ValueEventRow(
    int ItemId, string Kind, decimal QtyDelta, decimal ValueDeltaEur, decimal OffLedgerEur, DateOnly Date,
    int? ProfileId, string? DocumentType, string? DocumentId, string? DocumentNumber, string? Reason);

public sealed record StockProfileRef(int Id, string StockAccount, string Name, bool IsDefault);

public enum AgnumDocState { Sent, Unsent, Manual }

/// <summary>What Agnum holds (or should hold) for a purchase document, from the outbox and the document routes.</summary>
public sealed record AgnumDocFact(
    string DocumentType, int DocumentId, string Number, DateOnly Date, AgnumDocState State, string? Account, decimal AmountEur);

public sealed record MonthEndStatementRow(
    string StockAccount, string Name, decimal Opening, decimal Receipts, decimal TrueUps, decimal Issues, decimal Returns,
    decimal Adjustments, decimal Scrap, decimal SupplierReturns, decimal Revaluation, decimal OpeningEntries,
    decimal Closing, decimal PriceDifference, decimal Residual);

public sealed record BridgeDocument(
    string DocumentType, int DocumentId, string Number, DateOnly? Date, string Reason,
    decimal Agnum, decimal Warehouse, decimal Difference, decimal DifferenceAtStart);

public sealed record BridgeAccount(
    string StockAccount, string Name, decimal AgnumDebitTurnover, decimal WarehouseTurnover,
    decimal DifferenceAtStart, decimal DifferenceAtEnd, decimal Explained, decimal Unexplained,
    IReadOnlyList<BridgeDocument> AgnumMore, IReadOnlyList<BridgeDocument> WarehouseMore);

public sealed record ActRow(
    string Act, DateOnly Date, string? DocumentNumber, string Sku, decimal Qty, decimal ValueEur, string Reason, string StockAccount);

public sealed record ActGroup(string Act, string Reason, int Lines, decimal Qty, decimal ValueEur);

public sealed record MonthEndReport(
    int Year, int Month, IReadOnlyList<MonthEndStatementRow> Statement, MonthEndStatementRow Totals,
    IReadOnlyList<BridgeAccount> Bridge, IReadOnlyList<ActGroup> ActGroups, IReadOnlyList<ActRow> Acts)
{
    public decimal TotalUnexplained => Bridge.Sum(b => b.Unexplained);
}

/// <summary>
/// Blueprint 4.9: stock value statement, bridge to Agnum and acts of one month, computed from the value ledger.
/// Pure functions; the caller loads events and Agnum facts.
/// </summary>
public static class MonthEndStatement
{
    public const string NoAccount = "-";
    private static readonly string[] PurchaseDocumentTypes = ["InboundShipment", "AdditionalCostDocument", "SupplierCreditDocument"];
    private static readonly string[] PurchaseKinds =
        [StockValueKinds.Receipt, StockValueKinds.ReceiptTrueUp, StockValueKinds.PriceDifference, StockValueKinds.SupplierReturn, StockValueKinds.Revaluation];

    public static MonthEndReport Build(
        int year, int month, IReadOnlyList<ValueEventRow> events, IReadOnlyList<StockProfileRef> profiles,
        IReadOnlyList<AgnumDocFact> facts, IReadOnlyDictionary<int, string> skuByItem)
    {
        var from = new DateOnly(year, month, 1);
        var to = from.AddMonths(1).AddDays(-1);
        var resolver = new AccountResolver(profiles, facts);
        var inLedger = events.Where(e => e.Date <= to).ToList();
        var names = profiles.GroupBy(p => p.StockAccount).ToDictionary(g => g.Key, g => string.Join(" / ", g.Select(p => p.Name)));

        // ---- statement ----
        var rows = new List<MonthEndStatementRow>();
        foreach (var g in inLedger.GroupBy(resolver.Account).OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            decimal Sum(Func<ValueEventRow, bool> f) => Money(g.Where(f).Sum(e => e.ValueDeltaEur));
            decimal Month(params string[] kinds) => Sum(e => e.Date >= from && kinds.Contains(e.Kind));
            var opening = Sum(e => e.Date < from);
            var receipts = Month(StockValueKinds.Receipt);
            var trueUps = Month(StockValueKinds.ReceiptTrueUp);
            var issues = Month(StockValueKinds.Issue);
            var returns = Month(StockValueKinds.ReturnIn);
            var adjustments = Month(StockValueKinds.AdjustmentIn, StockValueKinds.AdjustmentOut);
            var scrap = Month(StockValueKinds.Scrap);
            var supplierReturns = Month(StockValueKinds.SupplierReturn);
            var revaluation = Month(StockValueKinds.Revaluation);
            var openingEntries = Month(StockValueKinds.Opening);
            var closing = Sum(_ => true);
            var priceDiff = Money(g.Where(e => e.Date >= from).Sum(e => e.OffLedgerEur));
            var computed = opening + receipts + trueUps + issues + returns + adjustments + scrap + supplierReturns + revaluation + openingEntries;
            rows.Add(new(g.Key, names.GetValueOrDefault(g.Key, string.Empty), opening, receipts, trueUps, issues, returns, adjustments,
                scrap, supplierReturns, revaluation, openingEntries, closing, priceDiff, Money(computed - closing)));
        }

        var totals = new MonthEndStatementRow("", "", rows.Sum(r => r.Opening), rows.Sum(r => r.Receipts), rows.Sum(r => r.TrueUps),
            rows.Sum(r => r.Issues), rows.Sum(r => r.Returns), rows.Sum(r => r.Adjustments), rows.Sum(r => r.Scrap),
            rows.Sum(r => r.SupplierReturns), rows.Sum(r => r.Revaluation), rows.Sum(r => r.OpeningEntries), rows.Sum(r => r.Closing),
            rows.Sum(r => r.PriceDifference), rows.Sum(r => r.Residual));

        // ---- bridge ----
        var purchase = inLedger.Where(e => PurchaseKinds.Contains(e.Kind) && e.DocumentType is not null
            && PurchaseDocumentTypes.Contains(e.DocumentType) && int.TryParse(e.DocumentId, out _)).ToList();
        var bridge = new List<BridgeAccount>();
        var accounts = purchase.Select(resolver.Account)
            .Concat(facts.Where(f => f.State == AgnumDocState.Sent && f.Account is not null).Select(f => f.Account!))
            .Distinct().OrderBy(a => a, StringComparer.Ordinal).ToList();
        foreach (var account in accounts)
        {
            var w = purchase.Where(e => resolver.Account(e) == account)
                .GroupBy(e => (Type: e.DocumentType!, Id: int.Parse(e.DocumentId!)))
                .ToDictionary(g => g.Key, g => g.ToList());
            var sent = facts.Where(f => f.State == AgnumDocState.Sent && f.Account == account)
                .GroupBy(f => (Type: f.DocumentType, Id: f.DocumentId)).ToDictionary(g => g.Key, g => g.ToList());
            var keys = w.Keys.Concat(sent.Keys).Distinct().ToList();

            decimal A(IEnumerable<AgnumDocFact>? f, DateOnly t) => f is null ? 0m : f.Where(x => x.Date <= t).Sum(x => x.AmountEur);
            decimal W(IEnumerable<ValueEventRow>? e, DateOnly t) => e is null ? 0m : e.Where(x => x.Date <= t).Sum(x => x.ValueDeltaEur + x.OffLedgerEur);

            var dayBefore = from.AddDays(-1);
            var docs = new List<BridgeDocument>();
            decimal explained = 0m, dStart = 0m, dEnd = 0m;
            foreach (var key in keys)
            {
                var fs = sent.GetValueOrDefault(key);
                var es = w.GetValueOrDefault(key);
                var aEnd = A(fs, to);
                var wEnd = W(es, to);
                var d0 = Money(A(fs, dayBefore) - W(es, dayBefore));
                var d1 = Money(aEnd - wEnd);
                explained += d1 - d0;
                dStart += d0;
                dEnd += d1;
                if (d0 == 0m && d1 == 0m) continue;

                var anyFact = facts.FirstOrDefault(f => f.DocumentType == key.Type && f.DocumentId == key.Id);
                var number = anyFact?.Number ?? es?.FirstOrDefault(e => !string.IsNullOrWhiteSpace(e.DocumentNumber))?.DocumentNumber ?? key.Id.ToString();
                docs.Add(new BridgeDocument(key.Type, key.Id, number, anyFact?.Date, Reason(d1, anyFact), Money(aEnd), Money(wEnd), d1, d0));
            }

            var turnover = Money(sent.Values.SelectMany(x => x).Where(f => f.Date >= from && f.Date <= to).Sum(f => f.AmountEur));
            var warehouse = Money(w.Values.SelectMany(x => x).Where(e => e.Date >= from).Sum(e => e.ValueDeltaEur + e.OffLedgerEur));
            explained = Money(explained);
            bridge.Add(new BridgeAccount(account, names.GetValueOrDefault(account, string.Empty), turnover, warehouse, Money(dStart), Money(dEnd),
                explained, Money(turnover - warehouse - explained),
                docs.Where(d => d.Difference > 0m).OrderBy(d => d.Number, StringComparer.Ordinal).ToList(),
                docs.Where(d => d.Difference < 0m).OrderBy(d => d.Number, StringComparer.Ordinal).ToList()));
        }

        // ---- acts ----
        var acts = inLedger.Where(e => e.Date >= from && e.Kind is StockValueKinds.Scrap or StockValueKinds.AdjustmentOut or StockValueKinds.AdjustmentIn)
            .OrderBy(e => e.Date).ThenBy(e => e.DocumentNumber, StringComparer.Ordinal)
            .Select(e => new ActRow(ActOf(e.Kind), e.Date, e.DocumentNumber, skuByItem.GetValueOrDefault(e.ItemId, e.ItemId.ToString()),
                Math.Abs(e.QtyDelta), Money(e.ValueDeltaEur), string.IsNullOrWhiteSpace(e.Reason) ? "-" : e.Reason!.Trim(), resolver.Account(e)))
            .ToList();
        var groups = acts.GroupBy(a => (a.Act, a.Reason)).OrderBy(g => g.Key.Act, StringComparer.Ordinal).ThenBy(g => g.Key.Reason, StringComparer.Ordinal)
            .Select(g => new ActGroup(g.Key.Act, g.Key.Reason, g.Count(), g.Sum(a => a.Qty), g.Sum(a => a.ValueEur))).ToList();

        return new MonthEndReport(year, month, rows, totals, bridge, groups, acts);
    }

    public static string ActOf(string kind) => kind switch
    {
        StockValueKinds.Scrap => "WriteOff",
        StockValueKinds.AdjustmentOut => "Shortage",
        _ => "Surplus"
    };

    private static string Reason(decimal difference, AgnumDocFact? fact)
        => difference > 0m ? "InvoicedNotReceived"
            : fact is null ? "NotInvoiced"
            : fact.State switch
            {
                AgnumDocState.Unsent => "NotSentToAgnum",
                AgnumDocState.Manual => "ManualInAgnum",
                _ => "AmountsDiffer"
            };

    private static decimal Money(decimal v) => decimal.Round(v, 2, MidpointRounding.AwayFromZero);

    /// <summary>Stock account of an event: its profile, else the account of the document it belongs to, else the default profile.</summary>
    private sealed class AccountResolver
    {
        private readonly Dictionary<int, string> _byProfile;
        private readonly Dictionary<(string, int), string> _byDocument = new();
        private readonly string _default;

        public AccountResolver(IReadOnlyList<StockProfileRef> profiles, IReadOnlyList<AgnumDocFact> facts)
        {
            _byProfile = profiles.ToDictionary(p => p.Id, p => p.StockAccount);
            _default = profiles.FirstOrDefault(p => p.IsDefault)?.StockAccount ?? NoAccount;
            foreach (var f in facts.Where(f => f.Account is not null))
                _byDocument[(f.DocumentType, f.DocumentId)] = f.Account!;
        }

        public string Account(ValueEventRow e)
        {
            if (e.ProfileId is { } id && _byProfile.TryGetValue(id, out var account)) return account;
            if (e.DocumentType is not null && int.TryParse(e.DocumentId, out var docId) && _byDocument.TryGetValue((e.DocumentType, docId), out var doc)) return doc;
            return _default;
        }
    }
}
