using System.Data;
using FluentAssertions;
using LKvitai.MES.Modules.Frontline.Infrastructure.Sql;
using Xunit;

namespace LKvitai.MES.Tests.Frontline.Unit;

public sealed class FabricReferencePriceMappingTests
{
    [Fact]
    public void MapReferencePrice_MapsSqlValuesAndNullableUnit()
    {
        var table = new DataTable();
        table.Columns.Add("TypeName", typeof(string));
        table.Columns.Add("Unit", typeof(string));
        table.Columns.Add("Price", typeof(decimal));
        table.Columns.Add("IsFrom", typeof(int));
        table.Rows.Add("R19", "1m pločio", 28.96m, 0);
        table.Rows.Add("R25 LP", DBNull.Value, 30.70m, 1);

        using var reader = table.CreateDataReader();
        reader.Read().Should().BeTrue();
        var first = SqlFabricQueryService.MapReferencePrice(reader);
        reader.Read().Should().BeTrue();
        var second = SqlFabricQueryService.MapReferencePrice(reader);

        first.TypeName.Should().Be("R19");
        first.Unit.Should().Be("1m pločio");
        first.Price.Should().Be(28.96m);
        first.IsFrom.Should().BeFalse();
        second.TypeName.Should().Be("R25 LP");
        second.Unit.Should().BeNull();
        second.Price.Should().Be(30.70m);
        second.IsFrom.Should().BeTrue();
    }
}
