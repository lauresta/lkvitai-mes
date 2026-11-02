using System.Globalization;
using LKvitai.MES.Modules.Sales.Infrastructure.Sql;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class LegacyProductCalculationRevisionTests
{
    private static IReadOnlyList<IReadOnlyList<IReadOnlyList<object?>>> Tables(params object?[][] rows)
        =>new IReadOnlyList<IReadOnlyList<object?>>[]{rows.Select(x=>(IReadOnlyList<object?>)x).ToArray()};
    private static string Hash(IReadOnlyList<IReadOnlyList<IReadOnlyList<object?>>> rows,int type=1,int colour=2,long? order=3)
        =>LegacyProductCalculationRevision.Compute(type,colour,order,rows);
    [Fact]
    public void Fingerprint_is_stable_culture_independent_and_binds_context()
    {
        var rows=Tables([1,"Width-45",114.000000m,true,new DateTime(2026,10,31)]);
        var expected=Hash(rows);var original=CultureInfo.CurrentCulture;
        try { CultureInfo.CurrentCulture=CultureInfo.GetCultureInfo("lt-LT");Assert.Equal(expected,Hash(rows)); }
        finally { CultureInfo.CurrentCulture=original; }
        Assert.Equal(64,expected.Length);
        Assert.NotEqual(expected,Hash(rows,type:4));Assert.NotEqual(expected,Hash(rows,colour:5));Assert.NotEqual(expected,Hash(rows,order:null));
    }
    [Theory]
    [InlineData("Width-45","Width-40")]
    [InlineData("fixed","any")]
    public void Formula_and_price_policy_changes_invalidate_the_same_displayed_configuration(string before,string after)
        =>Assert.NotEqual(Hash(Tables([before])),Hash(Tables([after])));
    [Fact]
    public void Numeric_reference_and_price_period_changes_invalidate_the_revision()
    {
        Assert.NotEqual(Hash(Tables([114m])),Hash(Tables([115m])));
        Assert.NotEqual(Hash(Tables([0.5m])),Hash(Tables([0.6m])));
        Assert.NotEqual(Hash(Tables([20.8526m])),Hash(Tables([20.8527m])));
        Assert.NotEqual(Hash(Tables([new DateTime(2026,10,31)])),Hash(Tables([new DateTime(2026,11,1)])));
    }
    [Fact]
    public void Typed_encoding_distinguishes_null_zero_text_and_collection_boundaries()
    {
        Assert.NotEqual(Hash(Tables(new object?[]{null})),Hash(Tables([0])));
        Assert.NotEqual(Hash(Tables([0])),Hash(Tables(["0"])));
        Assert.NotEqual(Hash(Tables([1])),Hash(Tables([1m])));
        Assert.NotEqual(Hash(Tables(["ab","c"])),Hash(Tables(["a","bc"])));
        Assert.NotEqual(Hash(Tables(["a"],["b"])),Hash(Tables(["a","b"])));
        Assert.NotEqual(Hash(Tables([1])),Hash(Tables([1],[1])));
    }
}
