using System.Globalization;
using System.Text.Json;
using Microsoft.Playwright;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.E2E.Ui;

// Run on deployed test. expected.json is refreshed from SELECT-only expected.sql immediately beforehand.
public sealed class DeployedPriceAuditTests
{
    private const string Origin = "https://cikada-test.lauresta.com";
    private const string Output = "/tmp/warehouse-price-audit";

    [Fact]
    public async Task CapturePriceScreensInBothLanguages()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAREHOUSE_QA_KEY")))
        {
            Console.WriteLine("Deployed price audit requires an explicit test credential.");
            return;
        }
        using var expected = JsonDocument.Parse(File.ReadAllText(Path.Combine(Output, "expected.json")));
        var ids = expected.RootElement.GetProperty("ids");
        string? Dynamic(string prefix, string key, string suffix = "") =>
            ids.GetProperty(key).ValueKind == JsonValueKind.Null ? null : prefix + ids.GetProperty(key) + suffix;
        var routes = new (string Name, string? Path)[]
        {
            ("dashboard", "/dashboard"), ("stock-dashboard", "/stock/dashboard"),
            ("available-stock", "/available-stock"), ("stock-report", "/reports/stock-level"),
            ("valuation", "/valuation/dashboard"), ("inbound-create", "/inbound/shipments/create"),
            ("sales-create", "/sales/orders/create"), ("sales-list", "/sales/orders"),
            ("agnum-balances", "/agnum/balances"), ("admin-items", "/admin/items"),
            ("admin-item-detail", Dynamic("/admin/items/", "item")),
            ("admin-lots", "/admin/lots"), ("agnum-reconcile", "/agnum/reconcile"),
            ("sales-allocations", "/sales/allocations"), ("cross-dock", "/cross-dock"),
            ("cycle-discrepancies", Dynamic("/cycle-counts/", "cycleCount", "/discrepancies")),
            ("cycle-execute", Dynamic("/cycle-counts/", "cycleCount", "/execute")),
            ("inbound-list", "/inbound/shipments"),
            ("inbound-detail", Dynamic("/inbound/shipments/", "inbound")),
            ("inbound-qc", "/inbound/qc"),
            ("outbound-detail", Dynamic("/outbound/orders/", "outbound")),
            ("outbound-pack", Dynamic("/outbound/pack/", "outbound")),
            ("picking", "/picking/tasks"), ("putaway", "/putaway"),
            ("pick-history", "/reports/pick-history"),
            ("receiving-history", "/reports/receiving-history"),
            ("stock-movements", "/reports/stock-movements"),
            ("traceability", "/reports/traceability"),
            ("reservations", "/reservations"), ("rmas", "/rmas"),
            ("sales-detail", Dynamic("/sales/orders/", "salesOrder")),
            ("adjustments", "/stock/adjustments"),
            ("location-balance", "/stock/location-balance"),
            ("transfer-create", "/transfers/create"),
            ("transfer-execute", Dynamic("/transfers/", "transfer", "/execute")),
            ("warehouse-3d", "/visualization/3d"),
            ("warehouse-2d", "/visualization/2d")
        };
        await File.WriteAllTextAsync(Path.Combine(Output, "run-start.txt"),
            DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture));
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Headless = true });
        var failures = new List<string>();
        foreach (var language in new[] { "en", "lt" })
        {
            await using var context = await browser.NewContextAsync(new()
            {
                ViewportSize = new() { Width = 1440, Height = 900 }, AcceptDownloads = true
            });
            await AuthorizeAsync(context, language);
            var directory = Path.Combine(Output, language);
            Directory.CreateDirectory(directory);
            var visitedRoutes = 0;
            foreach (var (name, path) in routes)
            {
                var onlyRoute = Environment.GetEnvironmentVariable("WAREHOUSE_PRICE_AUDIT_ROUTE");
                if (!string.IsNullOrWhiteSpace(onlyRoute) && !onlyRoute.Split(',').Contains(name)) continue;
                if (path is null)
                {
                    await File.WriteAllTextAsync(Path.Combine(directory, name + ".skip.txt"),
                        "No ID in test DB. No test record created.");
                    continue;
                }
                var page = await context.NewPageAsync();
                var consoleErrors = new List<string>();
                page.Console += (_, message) => { if (message.Type == "error") consoleErrors.Add(message.Text); };
                await ObserveErrorsAsync(page);
                try
                {
                    var response = await page.GotoAsync(Origin + "/warehouse" + path);
                    await WaitReadyAsync(page);
                    if (name == "available-stock")
                        await CheckAvailableStockAsync(page, expected.RootElement, language, failures);
                    var body = await page.Locator("body").InnerTextAsync();
                    if (name == "dashboard") CheckDashboard(body, expected.RootElement, language, failures);
                    if (name == "stock-report") CheckStockReport(body, expected.RootElement, language, failures);
                    if (name == "valuation") CheckValuation(body, expected.RootElement, language, failures);
                    if (name is "stock-movements" or "pick-history" or "adjustments" or "transfer-execute")
                    {
                        var label = language == "en" ? "Value (current cost)" : "Vertė (pagal dabartinę savikainą)";
                        if (!body.Contains(label, StringComparison.OrdinalIgnoreCase))
                            failures.Add($"{language}/{name}: movement value label does not identify current cost");
                    }
                    if (name == "agnum-balances")
                        await CheckAgnumAsync(page, expected.RootElement, language, failures);
                    if (name == "sales-create")
                        await CheckSalesPriceDefaultsAsync(page, expected.RootElement, language, failures);
                    if (name is "sales-detail" or "outbound-detail" or "outbound-pack")
                        await CheckSalesAmountsAsync(page, expected.RootElement, name, language, failures);
                    await File.WriteAllTextAsync(Path.Combine(directory, name + ".txt"), body);
                    await page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".png"), FullPage = true });
                    if (response?.Status != 200) failures.Add($"{language}/{name}: HTTP {response?.Status}");
                    if (page.Url.Contains("/portal", StringComparison.OrdinalIgnoreCase))
                        failures.Add($"{language}/{name}: redirected to Portal");
                    var uiErrors = await page.EvaluateAsync<string[]>("() => window.__warehouseAuditErrors || []");
                    failures.AddRange(uiErrors.Select(e => $"{language}/{name} UI: {e}"));
                    failures.AddRange(consoleErrors.Select(e => $"{language}/{name} console: {e}"));
                    Console.WriteLine($"{language}/{name}: HTTP {response?.Status}, chars {body.Length}, € {body.Count(c => c == '€')}");
                }
                catch (Exception ex)
                {
                    failures.Add($"{language}/{name}: {ex.GetType().Name}: {ex.Message}");
                    await page.ScreenshotAsync(new() { Path = Path.Combine(directory, name + ".failure.png"), FullPage = true });
                }
                finally { await page.CloseAsync(); }
                visitedRoutes++;
                if (visitedRoutes % 15 == 0 && string.IsNullOrWhiteSpace(onlyRoute))
                    await WaitForNextRateWindowAsync();
            }
            // API permits 100 requests per UTC minute per identity. Start the next culture
            // in a fresh server window so the audit does not manufacture 429 responses.
            if (language == "en" && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAREHOUSE_PRICE_AUDIT_ROUTE")))
                await WaitForNextRateWindowAsync();
        }
        await File.WriteAllLinesAsync(Path.Combine(Output, "browser-errors.txt"), failures);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static async Task AuthorizeAsync(IBrowserContext context, string language)
    {
        var key = Environment.GetEnvironmentVariable("WAREHOUSE_QA_KEY");
        Assert.False(string.IsNullOrWhiteSpace(key));
        var entry = await context.APIRequest.GetAsync(Origin + "/warehouse/", new()
        {
            Headers = new Dictionary<string, string> { ["X-Warehouse-QA-Key"] = key! }, MaxRedirects = 0
        });
        Assert.Equal(302, entry.Status);
        var culture = await context.APIRequest.GetAsync(
            Origin + "/warehouse/culture/set?culture=" + language + "&returnUrl=%2Fwarehouse%2Fdashboard",
            new() { MaxRedirects = 0 });
        Assert.Equal(302, culture.Status);
    }

    private static async Task WaitReadyAsync(IPage page)
    {
        await page.Locator("h1:visible,h2:visible,h3:visible,[data-testid]:visible,canvas:visible,.viz-refresh-age:visible").First.WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await page.Locator("body").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        // Server prerender is visible before Blazor attaches event handlers. Typing earlier
        // is discarded during hydration. Wait for the actual bound controls, not a delay.
        await page.WaitForFunctionAsync("""
            () => Array.from(document.querySelectorAll('button,input,select'))
              .some(e => Object.keys(e).some(k => k.startsWith('_blazorEvents_')))
            """, null, new() { Timeout = 30000 });
        await page.Locator(".spinner-border[role=status]").Last.WaitForAsync(new()
        {
            State = WaitForSelectorState.Hidden, Timeout = 30000
        });
    }

    private static async Task WaitForNextRateWindowAsync()
    {
        var now = DateTimeOffset.UtcNow;
        var next = DateTimeOffset.FromUnixTimeSeconds((now.ToUnixTimeSeconds() / 60 + 1) * 60 + 1);
        await Task.Delay(next - now);
    }

    private static string Price(JsonElement row, string property, string language)
    {
        var value = row.GetProperty(property);
        return value.ValueKind == JsonValueKind.Null ? "–" : ExpectedMoney(value.GetDecimal(), language);
    }

    private static string ExpectedMoney(decimal value, string language)
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo(language).Clone();
        if (language == "lt") culture.NumberFormat.NumberGroupSeparator = " ";
        culture.NumberFormat.NegativeSign = "-";
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("#,##0.00", culture) + " €";
    }

    private static void CheckDashboard(string body, JsonElement expected, string language, List<string> failures)
    {
        var positive = expected.GetProperty("stock").EnumerateArray()
            .Where(x => x.GetProperty("qty").GetDecimal() > 0 && !x.GetProperty("virtual").GetBoolean()).ToList();
        var quantity = positive.Sum(x => x.GetProperty("qty").GetDecimal());
        var skus = positive.Select(x => x.GetProperty("sku").GetString()).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        if (!body.Contains($"\n{quantity:0.###}\n", StringComparison.Ordinal) ||
            !body.Contains($"\n{skus}\n", StringComparison.Ordinal))
            failures.Add($"{language}/dashboard: SKU/quantity differs from DB ({skus}/{quantity})");
        var total = positive.Where(x => x.GetProperty("value").ValueKind != JsonValueKind.Null)
            .Sum(x => x.GetProperty("value").GetDecimal());
        if (!body.Contains(ExpectedMoney(total, language), StringComparison.Ordinal))
            failures.Add($"{language}/dashboard: priced-row cost total differs from DB ({total})");
    }

    private static void CheckStockReport(string body, JsonElement expected, string language, List<string> failures)
    {
        var lines = body.Split('\n');
        foreach (var row in expected.GetProperty("stock").EnumerateArray().Where(x => !x.GetProperty("virtual").GetBoolean()))
        {
            var sku = row.GetProperty("sku").GetString();
            var location = row.GetProperty("location").GetString();
            var qty = row.GetProperty("qty").GetDecimal().ToString("0.###", CultureInfo.InvariantCulture);
            var match = lines.FirstOrDefault(x => x.StartsWith(sku + "\t", StringComparison.OrdinalIgnoreCase)
                && x.Contains("\t" + location + "\t", StringComparison.Ordinal)
                && x.Contains("\t" + qty + "\t", StringComparison.Ordinal));
            if (match is null || !match.Contains(Price(row, "cost", language), StringComparison.Ordinal) ||
                !match.Contains(Price(row, "sale", language), StringComparison.Ordinal) ||
                !match.EndsWith(Price(row, "value", language), StringComparison.Ordinal))
                failures.Add($"{language}/stock-report: DB price mismatch {sku}/{location}/{qty}");
        }
        if (lines.Any(x => x.Contains("\tAGNUM\t", StringComparison.Ordinal)))
            failures.Add($"{language}/stock-report: virtual AGNUM rows shown with Include virtual off");
    }

    private static void CheckValuation(string body, JsonElement expected, string language, List<string> failures)
    {
        var row = expected.GetProperty("stock").EnumerateArray().Single(x =>
            x.GetProperty("sku").GetString() == "000-000-101" &&
            x.GetProperty("location").GetString() == "R3-C6-L1-B1");
        var match = body.Split('\n').FirstOrDefault(x => x.StartsWith("000-000-101 -", StringComparison.Ordinal));
        var qty = row.GetProperty("qty").GetDecimal().ToString("0.###", CultureInfo.InvariantCulture);
        if (match is null || !match.Contains("\t" + qty + "\t", StringComparison.Ordinal) ||
            !match.EndsWith(Price(row, "value", language), StringComparison.Ordinal))
            failures.Add($"{language}/valuation: 000-000-101 differs from DB ({qty} × {Price(row, "cost", language)} = {Price(row, "value", language)}); UI: {match}");
    }

    private static async Task CheckAvailableStockAsync(IPage page, JsonElement expected, string language, List<string> failures)
    {
        foreach (var sku in expected.GetProperty("stock").EnumerateArray()
                     .Select(x => x.GetProperty("sku").GetString()!).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            await page.Locator("[data-testid=stock-search] input").FillAsync(sku);
            await page.Locator("[data-testid=stock-search] input").PressAsync("Tab");
            await page.Locator("[data-testid=stock-search-btn]").ClickAsync();
            await page.Locator("[data-testid=stock-grid], [data-testid=stock-validation-error]")
                .WaitForAsync(new() { State = WaitForSelectorState.Visible });
            if (await page.Locator("[data-testid=stock-validation-error]").IsVisibleAsync())
            {
                failures.Add($"{language}/available-stock: search validation for {sku}: " +
                    await page.Locator("[data-testid=stock-validation-error]").InnerTextAsync());
                continue;
            }
            await page.WaitForFunctionAsync("sku => Array.from(document.querySelectorAll('[data-testid=stock-grid] tbody tr')).some(tr => tr.cells[2]?.textContent?.trim().toLowerCase() === sku.toLowerCase())", sku,
                new() { Timeout = 15000 });
            await page.Locator(".spinner-border[role=status]").Last.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            var rows = await page.Locator("[data-testid=stock-grid] tbody tr").AllAsync();
            var actual = new List<string[]>();
            foreach (var tr in rows) actual.Add((await tr.Locator("td").AllTextContentsAsync()).Select(x => x.Trim()).ToArray());
            foreach (var row in expected.GetProperty("stock").EnumerateArray().Where(x =>
                         x.GetProperty("sku").GetString()?.Equals(sku, StringComparison.OrdinalIgnoreCase) == true &&
                         !x.GetProperty("virtual").GetBoolean() && x.GetProperty("qty").GetDecimal() > 0))
            {
                var warehouse = row.GetProperty("warehouse").GetString();
                var location = row.GetProperty("location").GetString();
                var match = actual.FirstOrDefault(x => x.Length >= 11 && x[0] == warehouse && x[1] == location && x[2] == sku);
                if (match is null || match[8] != Price(row, "cost", language) || match[9] != Price(row, "sale", language) ||
                    match[10] != Price(row, "value", language))
                    failures.Add($"{language}/available-stock: DB price mismatch {sku}/{warehouse}/{location}: {string.Join("|", match ?? [])}");
            }
            if (actual.Any(x => x.Length >= 2 && (x[1] == "AGNUM" || x[1].StartsWith("IN_TRANSIT_", StringComparison.Ordinal) || x[1] == "SHIPPING")))
                failures.Add($"{language}/available-stock: virtual row shown with Include virtual off for {sku}");
        }
    }

    private static async Task CheckAgnumAsync(IPage page, JsonElement expected, string language, List<string> failures)
    {
        foreach (var group in expected.GetProperty("agnum").EnumerateArray().GroupBy(x => x.GetProperty("sndId").GetInt32()))
        {
            await page.Locator("select.form-select").First.SelectOptionAsync(group.Key.ToString(CultureInfo.InvariantCulture));
            await page.Locator(".spinner-border[role=status]").Last.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            foreach (var row in group)
            {
                var sku = row.GetProperty("sku").GetString()!;
                await page.Locator("input.form-control").First.FillAsync(sku);
                var qty = row.GetProperty("qty").GetDecimal().ToString("0.###", CultureInfo.InvariantCulture);
                try
                {
                    await page.WaitForFunctionAsync("([sku,qty]) => Array.from(document.querySelectorAll('table tbody tr')).some(tr => tr.cells[0]?.textContent?.trim().toLowerCase() === sku.toLowerCase() && tr.cells[3]?.textContent?.trim() === qty)", new[] { sku, qty }, new() { Timeout = 15000 });
                    await page.WaitForFunctionAsync("([sku,cost]) => Array.from(document.querySelectorAll('table tbody tr')).some(tr => tr.cells[0]?.textContent?.trim().toLowerCase() === sku.toLowerCase() && tr.cells[4]?.textContent?.trim() === cost)", new[] { sku, Price(row, "cost", language) }, new() { Timeout = 15000 });
                }
                catch (TimeoutException)
                {
                    failures.Add($"{language}/agnum-balances: SND {group.Key} SKU {sku} qty {qty} did not load from latest import");
                    continue;
                }
                var matches = await page.Locator("table tbody tr").AllAsync();
                var cells = new List<string[]>();
                foreach (var tr in matches) cells.Add((await tr.Locator("td").AllTextContentsAsync()).Select(x => x.Trim()).ToArray());
                var match = cells.Where(x => x.Length >= 7 && x[0].Equals(sku, StringComparison.OrdinalIgnoreCase)).ToList();
                if (match.Count != 1 || match[0][4] != Price(row, "cost", language) || match[0][5] != Price(row, "sale", language) ||
                    match[0][6] != Price(row, "costValue", language))
                    failures.Add($"{language}/agnum-balances: DB price mismatch or duplicate SND {group.Key} SKU {sku}");
            }
        }
    }

    private static async Task CheckSalesAmountsAsync(IPage page, JsonElement expected, string name,
        string language, List<string> failures)
    {
        var orderId = expected.GetProperty("ids").GetProperty("salesOrder").GetString();
        foreach (var line in expected.GetProperty("sales").EnumerateArray()
                     .Where(x => x.GetProperty("orderId").GetString() == orderId))
        {
            var sku = line.GetProperty("sku").GetString();
            var amount = Price(line, "calculatedLineAmount", language);
            var price = Price(line, "unitPrice", language);
            if (line.GetProperty("lineAmount").GetDecimal() != line.GetProperty("calculatedLineAmount").GetDecimal())
                failures.Add($"{language}/{name}: stored sales line amount differs from qty × price for {sku}");
            var rows = await page.Locator("table tbody tr").AllAsync();
            var matching = new List<string>();
            foreach (var row in rows)
            {
                var text = await row.InnerTextAsync();
                if (text.Contains(sku ?? "\u0000", StringComparison.OrdinalIgnoreCase)) matching.Add(text);
            }
            if (matching.Count != 1 || !matching[0].Contains(price, StringComparison.Ordinal) ||
                !matching[0].Contains(amount, StringComparison.Ordinal))
                failures.Add($"{language}/{name}: sales price/amount differs from DB for {sku}; expected {price}/{amount}");
        }
    }

    private static async Task CheckSalesPriceDefaultsAsync(IPage page, JsonElement expected,
        string language, List<string> failures)
    {
        var customers = await page.Locator("#customer option").AllAsync();
        var ids = new List<string>();
        foreach (var option in customers)
        {
            var value = await option.GetAttributeAsync("value");
            if (Guid.TryParse(value, out _)) ids.Add(value!);
        }
        if (ids.Count < 2)
        {
            failures.Add($"{language}/sales-create: fewer than two selectable customers");
            return;
        }

        var priced = expected.GetProperty("agnum").EnumerateArray().First(x =>
            x.GetProperty("sku").GetString() == "0000001");
        var pricedItemId = priced.GetProperty("itemId").GetInt32().ToString(CultureInfo.InvariantCulture);
        var price = priced.GetProperty("sale").GetDecimal().ToString("0.00", CultureInfo.InvariantCulture);
        var itemSelect = page.Locator("table tbody tr select").First;
        var priceInput = page.Locator("table tbody tr input[type=number]").Nth(1);
        await page.Locator("#customer").SelectOptionAsync(ids[0]);
        await itemSelect.SelectOptionAsync(pricedItemId);
        await page.WaitForFunctionAsync("expected => Number(Array.from(document.querySelectorAll('table tbody tr input[type=number]'))[1]?.value) === Number(expected)", price,
            new() { Timeout = 10000 });
        await page.WaitForFunctionAsync("([cost,sale]) => { const hint = document.querySelector('table tbody tr td:first-child .small')?.textContent || ''; return hint.includes(cost) && hint.includes(sale); }",
            new[] { Price(priced, "cost", language), Price(priced, "sale", language) }, new() { Timeout = 10000 });
        await page.Locator("#customer").SelectOptionAsync(ids[1]);
        await page.WaitForFunctionAsync("expected => Number(Array.from(document.querySelectorAll('table tbody tr input[type=number]'))[1]?.value) === Number(expected)", price,
            new() { Timeout = 10000 });
        if (!decimal.TryParse(await priceInput.InputValueAsync(), NumberStyles.Number,
                CultureInfo.InvariantCulture, out var actualPrice) || actualPrice != priced.GetProperty("sale").GetDecimal())
            failures.Add($"{language}/sales-create: BasePrice not retained after customer change");

        var noPriceItemId = expected.GetProperty("ids").GetProperty("item").GetInt32().ToString(CultureInfo.InvariantCulture);
        await itemSelect.SelectOptionAsync(noPriceItemId);
        await page.WaitForFunctionAsync("() => Array.from(document.querySelectorAll('table tbody tr input[type=number]'))[1]?.value === ''",
            null, new() { Timeout = 10000 });
        if (await priceInput.InputValueAsync() != string.Empty)
            failures.Add($"{language}/sales-create: item without BasePrice did not require manual input");
    }

    private static Task ObserveErrorsAsync(IPage page) => page.AddInitScriptAsync("""
        (() => {
          window.__warehouseAuditErrors = [];
          const check = () => {
            for (const el of document.querySelectorAll('#blazor-error-ui, .toast.bg-danger, .alert-danger, .mud-alert-error, [data-testid=shared-error-banner], .lk-state--error')) {
              const style = getComputedStyle(el);
              if (style.display === 'none' || style.visibility === 'hidden') continue;
              const message = (el.textContent || '').trim();
              if (message && !window.__warehouseAuditErrors.includes(message)) window.__warehouseAuditErrors.push(message);
            }
          };
          document.addEventListener('DOMContentLoaded', () => {
            new MutationObserver(check).observe(document.body, {childList: true, subtree: true, attributes: true});
            check();
          });
        })();
        """);
}
