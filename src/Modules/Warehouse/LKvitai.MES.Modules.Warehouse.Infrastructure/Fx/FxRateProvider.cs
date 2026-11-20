using System.Globalization;
using System.Xml.Linq;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;

public enum FxRateStatus
{
    Ok,
    /// <summary>The document date is today and the Bank of Lithuania has not published today's rate yet.</summary>
    NotPublishedToday,
    Unavailable
}

public sealed record FxRateQuote(decimal UnitsPerEur, DateOnly RateDate, string Source);

public sealed record FxRateResult(FxRateStatus Status, FxRateQuote? Quote, string? Message)
{
    public static FxRateResult Ok(FxRateQuote quote) => new(FxRateStatus.Ok, quote, null);
}

/// <summary>Blueprint 4.3: the Bank of Lithuania rate, units of currency per 1 EUR, published on a date.</summary>
public interface IFxRateProvider
{
    Task<FxRateResult> GetRateAsync(string currency, DateOnly date, CancellationToken ct = default);

    /// <summary>Last cached rate of the currency published before <paramref name="date"/>; used by the sanity check.</summary>
    Task<decimal?> GetPreviousRateAsync(string currency, DateOnly date, CancellationToken ct = default);
}

/// <summary>
/// Reads https://www.lb.lt/webservices/FxRates/FxRates.asmx/getFxRates?tp=EU&amp;dt=YYYY-MM-DD and caches every
/// currency of the answer in fx_rates. For a day without publication the service itself answers with the last
/// published day, which is exactly the rule of the day of blueprint 4.3.
/// </summary>
public sealed class LbFxRateProvider : IFxRateProvider
{
    public const string SourceName = "BankOfLithuania";

    private readonly HttpClient _http;
    private readonly WarehouseDbContext _db;
    private readonly ILogger<LbFxRateProvider> _logger;
    private readonly Func<DateOnly> _today;

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public LbFxRateProvider(HttpClient http, WarehouseDbContext db, ILogger<LbFxRateProvider> logger)
        : this(http, db, logger, () => DateOnly.FromDateTime(Domain.Aggregates.StockBusinessDate.From(DateTime.UtcNow)))
    {
    }

    public LbFxRateProvider(HttpClient http, WarehouseDbContext db, ILogger<LbFxRateProvider> logger, Func<DateOnly> today)
    {
        _http = http;
        _db = db;
        _logger = logger;
        _today = today;
    }

    public async Task<FxRateResult> GetRateAsync(string currency, DateOnly date, CancellationToken ct = default)
    {
        currency = currency.Trim().ToUpperInvariant();
        if (currency == "EUR")
        {
            return FxRateResult.Ok(new FxRateQuote(1m, date, "EUR"));
        }

        var cached = await _db.FxRates.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Currency == currency && x.RateDate == date, ct);
        if (cached is not null)
        {
            return FxRateResult.Ok(new FxRateQuote(cached.UnitsPerEur, cached.RateDate, SourceName));
        }

        List<(DateOnly Date, string Ccy, decimal Units)> rows;
        try
        {
            var url = $"getFxRates?tp=EU&dt={date:yyyy-MM-dd}";
            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            rows = Parse(await response.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or System.Xml.XmlException or FormatException)
        {
            _logger.LogWarning(ex, "Bank of Lithuania rate request for {Date} failed.", date);
            return new FxRateResult(FxRateStatus.Unavailable, null, "Exchange rate service is not available.");
        }

        if (rows.Count == 0)
        {
            return new FxRateResult(FxRateStatus.Unavailable, null, "Exchange rate service returned no rates.");
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var group in rows.GroupBy(r => r.Date))
        {
            var existing = await _db.FxRates.Where(x => x.RateDate == group.Key).Select(x => x.Currency).ToListAsync(ct);
            foreach (var r in group.Where(r => !existing.Contains(r.Ccy)))
            {
                _db.FxRates.Add(new FxRate { RateDate = r.Date, Currency = r.Ccy, UnitsPerEur = r.Units, LoadedAt = now });
            }
        }

        await _db.SaveChangesAsync(ct);

        var match = rows.Where(r => r.Ccy == currency).OrderByDescending(r => r.Date).FirstOrDefault();
        if (match == default)
        {
            return new FxRateResult(FxRateStatus.Unavailable, null, $"No rate for {currency} was published.");
        }

        if (date >= _today() && match.Date < date)
        {
            return new FxRateResult(FxRateStatus.NotPublishedToday, null, "The rate for today is not published yet.");
        }

        return FxRateResult.Ok(new FxRateQuote(match.Units, match.Date, SourceName));
    }

    public async Task<decimal?> GetPreviousRateAsync(string currency, DateOnly date, CancellationToken ct = default)
    {
        currency = currency.Trim().ToUpperInvariant();
        var row = await _db.FxRates.AsNoTracking()
            .Where(x => x.Currency == currency && x.RateDate < date)
            .OrderByDescending(x => x.RateDate)
            .FirstOrDefaultAsync(ct);
        return row?.UnitsPerEur;
    }

    /// <summary>Parses the LB answer; a block without a date, currency or amount is a format error.</summary>
    public static List<(DateOnly Date, string Ccy, decimal Units)> Parse(string xml)
    {
        var doc = XDocument.Parse(xml);
        var result = new List<(DateOnly, string, decimal)>();
        foreach (var rate in doc.Descendants().Where(e => e.Name.LocalName == "FxRate"))
        {
            var dt = rate.Elements().FirstOrDefault(e => e.Name.LocalName == "Dt")?.Value;
            var date = DateOnly.ParseExact(dt ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            foreach (var amount in rate.Elements().Where(e => e.Name.LocalName == "CcyAmt"))
            {
                var ccy = amount.Elements().First(e => e.Name.LocalName == "Ccy").Value.Trim().ToUpperInvariant();
                if (ccy == "EUR")
                {
                    continue;
                }

                var amt = decimal.Parse(
                    amount.Elements().First(e => e.Name.LocalName == "Amt").Value,
                    NumberStyles.Number, CultureInfo.InvariantCulture);
                result.Add((date, ccy, amt));
            }
        }

        return result;
    }
}
