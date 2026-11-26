using LKvitai.MES.Modules.Finance.WebUI.IncomesReport;
using Xunit;

namespace LKvitai.MES.Tests.Finance.Unit;

public class IncomesPageTests
{
    private const string Nbsp = " ";
    private static readonly DateOnly Today = new(2026, 11, 26);

    [Theory]
    [InlineData(1240, "1" + Nbsp + "240,00" + Nbsp + "€")]
    [InlineData(461313.07, "461" + Nbsp + "313,07" + Nbsp + "€")]
    [InlineData(0, "0,00" + Nbsp + "€")]
    [InlineData(-1050.06, "−1" + Nbsp + "050,06" + Nbsp + "€")]
    [InlineData(-0.001, "0,00" + Nbsp + "€")]
    public void Money_uses_the_lithuanian_format(double value, string expected) =>
        Assert.Equal(expected, IncomesFormat.Money((decimal)value));

    [Fact]
    public void Visuals_use_whole_euros_and_signed_percent()
    {
        Assert.Equal("15" + Nbsp + "492" + Nbsp + "€", IncomesFormat.MoneyWhole(15491.5m));
        Assert.Equal("−10,5" + Nbsp + "%", IncomesFormat.Signed(-10.46m));
        Assert.Equal("+5,2" + Nbsp + "%", IncomesFormat.Signed(5.2m));
        Assert.Equal("0,0" + Nbsp + "%", IncomesFormat.Signed(0.04m));
        Assert.Equal("1" + Nbsp + "351", IncomesFormat.Count(1351));
        Assert.Equal("51" + Nbsp + "%", IncomesFormat.Percent("51"));
        Assert.Equal("7,2" + Nbsp + "%", IncomesFormat.Percent("7.2"));
        Assert.Null(IncomesFormat.Change(10, 0));
    }

    [Theory]
    [InlineData(1, "One")]
    [InlineData(21, "One")]
    [InlineData(11, "Many")]
    [InlineData(2, "Few")]
    [InlineData(9, "Few")]
    [InlineData(22, "Few")]
    [InlineData(12, "Many")]
    [InlineData(19, "Many")]
    [InlineData(0, "Many")]
    [InlineData(10, "Many")]
    [InlineData(20, "Many")]
    [InlineData(111, "Many")]
    public void Lithuanian_plural_forms(long count, string expected) =>
        Assert.Equal(expected, IncomesFormat.PluralForm(count));

    [Fact]
    public void Url_state_round_trips()
    {
        var state = IncomesUrlState.Default(Today) with
        {
            Branches = ["KNS", "VLN"],
            Methods = ["BANK", "EKA"],
            Group = "branch",
            Sort = "amt",
            SortDesc = true,
            Day = new DateOnly(2026, 10, 14),
            DrillBranch = "KNS",
            DrillMethod = "EKA",
            Week = 41,
            Refunds = true,
            NoEmployee = true,
            Search = "UAB „Pavyzdys“ & co",
        };
        var query = state.ToQuery();
        Assert.StartsWith("from=2026-10-01&to=2026-10-31&branch=KNS,VLN&method=BANK,EKA&group=branch&sort=-amt&day=2026-10-14", query);

        var parsed = IncomesUrlState.Parse(IncomesUrlState.QueryOf("https://cikada/finance/incomes?" + query), Today);
        Assert.Equal(query, parsed.ToQuery());
        Assert.Equal(state.Search, parsed.Search);
        Assert.Equal(state.Branches, parsed.Branches);
        Assert.Equal((state.Day, state.Week, state.Refunds, state.NoEmployee), (parsed.Day, parsed.Week, parsed.Refunds, parsed.NoEmployee));
    }

    [Fact]
    public void Url_state_omits_defaults_and_keeps_orders_specific_filters_apart()
    {
        Assert.Equal("from=2026-10-01&to=2026-10-31", IncomesUrlState.Default(Today).ToQuery());

        var orders = IncomesUrlState.Parse(IncomesUrlState.QueryOf("/finance/incomes?from=2026-01-01&to=2026-11-26&ds=orders&cancelled=1&refunds=1&method=BANK"), Today);
        Assert.True(orders.Orders);
        Assert.True(orders.Cancelled);
        Assert.False(orders.Refunds);
        Assert.Null(orders.Methods);
        Assert.Equal("month", orders.EffectiveGroup);
        Assert.Equal("from=2026-01-01&to=2026-11-26&ds=orders&cancelled=1", orders.ToQuery());

        var broken = IncomesUrlState.Parse(IncomesUrlState.QueryOf("?from=2026-10-31&to=2026-10-01&dWeek=99"), Today);
        Assert.Equal(IncomesUrlState.Default(Today).ToQuery(), broken.ToQuery());
    }

    [Fact]
    public void Presets_are_derived_from_the_period()
    {
        Assert.Equal("last", IncomesPresets.Detect(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 31), Today));
        Assert.Equal("this", IncomesPresets.Detect(new DateOnly(2026, 11, 1), Today, Today));
        Assert.Equal("q", IncomesPresets.Detect(new DateOnly(2026, 10, 1), Today, Today));
        Assert.Equal("y", IncomesPresets.Detect(new DateOnly(2026, 1, 1), Today, Today));
        Assert.Equal("custom", IncomesPresets.Detect(new DateOnly(2026, 10, 2), new DateOnly(2026, 10, 31), Today));
        Assert.Equal((new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)), IncomesPresets.StepMonth(new DateOnly(2026, 10, 1), -1, Today));
        Assert.Equal((new DateOnly(2026, 11, 1), Today), IncomesPresets.StepMonth(new DateOnly(2026, 10, 1), 1, Today));
        Assert.Null(IncomesPresets.StepMonth(new DateOnly(2026, 11, 1), 1, Today));
    }

    [Fact]
    public void Toggling_chips_collapses_to_all()
    {
        string[] all = ["KNS", "VLN"];
        Assert.Equal(["VLN"], IncomesUrlState.Toggle(null, "VLN", all));
        Assert.Null(IncomesUrlState.Toggle(["VLN"], "VLN", all));
        Assert.Null(IncomesUrlState.Toggle(["VLN"], "KNS", all));
    }

    [Fact]
    public async Task Grouped_rows_are_laid_out_with_headers_and_collapsed_groups()
    {
        IncomesRowDto Row(int i) => new($"M{i}", new DateOnly(2026, 10, 1), null, $"VV-{i}-26", "Vilnius", "VLN", "VV",
            "BANK", 10, null, null, null, null, null, null, null, null, null, null, null);
        var rows = Enumerable.Range(0, 250).Select(Row).ToList();
        var groups = new List<IncomesGroupDto> { new("VLN", "Vilnius", 200, 2000), new("KNS", "Kaunas", 50, 500) };
        var calls = new List<(int Skip, int Take)>();
        var source = new IncomesRowSource((skip, take, _) =>
        {
            calls.Add((skip, take));
            return Task.FromResult(new IncomesRowsPageDto(250, 2500, 0, groups, rows.Skip(skip).Take(take).ToList()));
        });
        await source.LoadAsync(CancellationToken.None);
        Assert.Equal(252, source.FlatCount);

        var window = await source.RangeAsync(199, 4, CancellationToken.None);
        Assert.Equal(["M198", "M199", "group:KNS", "M200"],
            window.Select(x => x.Group is null ? x.Row!.Id : "group:" + x.Group.Key).ToArray());
        Assert.Contains((200, 100), calls);

        source.Toggle("VLN");
        Assert.Equal(52, source.FlatCount);
        var collapsed = await source.RangeAsync(0, 3, CancellationToken.None);
        Assert.True(collapsed[0].Collapsed);
        Assert.Equal(["group:VLN", "group:KNS", "M200"],
            collapsed.Select(x => x.Group is null ? x.Row!.Id : "group:" + x.Group.Key).ToArray());
    }
}
