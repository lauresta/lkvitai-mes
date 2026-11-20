using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing;

public sealed record PostingPayloadInput(
    Guid Key, int Revision, string DocumentNumber, DateOnly DocumentDate, long ClientId, long SndId, long SchemeId,
    string Currency, decimal UnitsPerEur, decimal Net, decimal Vat, string? VatCode,
    string StockAccount, IReadOnlyList<(string Account, decimal Amount)> Expenses, int PaymentTermDays = 0);

public sealed record BuiltPayload(string Json, string Hash, AgnumPurchasePostingRequest Request);

/// <summary>Blueprint 5.1 request, built with the money rules of section 3 (eur = Round(amount x rateEurPerUnit, 2)).</summary>
public static class AgnumPayloadBuilder
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static BuiltPayload Build(PostingPayloadInput input)
    {
        var rate = PurchaseMath.RateEurPerUnit(input.Currency == "EUR" ? 1m : input.UnitsPerEur);
        var expenseByAccount = input.Expenses
            .GroupBy(e => e.Account.Trim(), StringComparer.Ordinal)
            .Select(g => (Account: g.Key, Amount: g.Sum(x => x.Amount)))
            .Where(x => x.Amount != 0m)
            .ToList();
        var stock = input.Net - expenseByAccount.Sum(x => x.Amount);

        var postings = new List<AgnumPostingLine>();
        if (stock != 0m)
            postings.Add(Line(input.StockAccount, stock, rate));
        foreach (var e in expenseByAccount)
            postings.Add(Line(e.Account, e.Amount, rate));

        var request = new AgnumPurchasePostingRequest
        {
            Key = input.Key.ToString("D"),
            Revision = input.Revision,
            Kind = "invoice",
            DocNo = input.DocumentNumber,
            DocDate = input.DocumentDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ClientId = input.ClientId,
            SndId = input.SndId,
            SchemeId = input.SchemeId,
            Currency = input.Currency,
            RateEurPerUnit = rate,
            Net = decimal.Round(input.Net, 2),
            Vat = decimal.Round(input.Vat, 2),
            Gross = decimal.Round(input.Net + input.Vat, 2),
            VatCode = input.VatCode ?? string.Empty,
            Postings = postings,
            PaymentTermDays = input.PaymentTermDays
        };

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(request, Json)))).ToLowerInvariant();
        request.PayloadHash = hash;
        return new BuiltPayload(JsonSerializer.Serialize(request, Json), hash, request);
    }

    private static AgnumPostingLine Line(string account, decimal amount, decimal rate) => new()
    {
        DebitAccount = account,
        Amount = decimal.Round(amount, 2),
        AmountEur = PurchaseMath.ToEur(amount, rate)
    };
}
