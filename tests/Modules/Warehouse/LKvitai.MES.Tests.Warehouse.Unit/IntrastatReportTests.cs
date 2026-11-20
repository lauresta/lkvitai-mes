using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Domain.Purchasing;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

public sealed class IntrastatReportTests
{
    [Fact]
    public void MonthWithTwoCountriesAndThreeCodesMatchesHandCalculation()
    {
        var rows = IntrastatReport.Build(new[]
        {
            new IntrastatLineInput("84713000", "PL", 10m, 100.25m, 0.5m),
            new IntrastatLineInput("84713000", "pl", 4m, 40.10m, 0.5m),
            new IntrastatLineInput("39269097", "PL", 3m, 30.00m, 1.2m),
            new IntrastatLineInput("84713000", "DE", 5m, 75.55m, 0.5m),
            new IntrastatLineInput("73181500", "DE", 100m, 20.00m, 0.01m),
        });

        rows.Select(r => (r.Country, r.CommodityCode, r.ValueEur, r.NetMassKg, r.Quantity)).Should().Equal(
            ("DE", "73181500", 20.00m, 1.000m, 100m),
            ("DE", "84713000", 75.55m, 2.500m, 5m),
            ("PL", "39269097", 30.00m, 3.600m, 3m),
            ("PL", "84713000", 140.35m, 7.000m, 14m));
        rows.Sum(r => r.ValueEur).Should().Be(265.90m);
        rows.Sum(r => r.NetMassKg).Should().Be(14.100m);
        rows.Sum(r => r.Quantity).Should().Be(122m);
    }

    [Fact]
    public void CsvHasHeaderAndInvariantNumbers()
    {
        var csv = IntrastatReport.ToCsv(IntrastatReport.Build(new[]
        {
            new IntrastatLineInput("84713000", "PL", 2m, 10m, 0.25m)
        }));

        csv.Should().Be("CommodityCode;DispatchCountry;ValueEur;NetMassKg;Quantity\r\n84713000;PL;10.00;0.500;2\r\n");
    }
}
