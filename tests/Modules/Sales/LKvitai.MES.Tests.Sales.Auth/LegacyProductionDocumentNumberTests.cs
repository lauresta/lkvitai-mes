using LKvitai.MES.Modules.Sales.Application.OrderEntry;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
using Xunit;
namespace LKvitai.MES.Tests.Sales.Auth;
public sealed class LegacyProductionDocumentNumberTests
{
    [Fact]
    public void Longest_prefix_preserves_all128_guid_bits_within_downstream32_characters()
    {
        var id=Guid.Parse("11111111-2222-3333-4444-555555555555");
        var number=LegacyProductionDocumentNumber.Create("sales",id);
        Assert.Equal(32,number.Length);Assert.Equal(32,LegacyProductionDocumentNumber.MaximumCharacters);
        Assert.StartsWith("SALES-",number);Assert.Equal(CanonicalBits(id),DecodeBits(number[6..]));
        Assert.Equal(number,LegacyProductionDocumentNumber.Create("SALES",id));
    }
    [Fact]
    public void Every_guid_bit_changes_the_identifier_without_truncating_high_or_low_bits()
    {
        var identifiers=new HashSet<string>();
        for(var bit=0;bit<128;bit++)
        {
            var bytes=new byte[16];bytes[bit/8]=(byte)(1<<(7-bit%8));var id=Guid.Parse(Convert.ToHexString(bytes));
            var number=LegacyProductionDocumentNumber.Create("T",id);
            Assert.Equal(28,number.Length);Assert.True(identifiers.Add(number));
            Assert.Equal(CanonicalBits(id),DecodeBits(number[2..]));
        }
        var full=Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");
        Assert.Equal(CanonicalBits(full),DecodeBits(LegacyProductionDocumentNumber.Create("TEST5",full)[6..]));
    }
    [Theory]
    [InlineData("")][InlineData("123456")][InlineData("SPG'; DROP")][InlineData("Ž")][InlineData("ABC\n")]
    public void Unverified_prefixes_are_configuration_errors(string prefix)
        =>Assert.Throws<OrderEntryException>(()=>LegacyProductionDocumentNumber.Create(prefix,Guid.NewGuid()));
    [Fact]
    public void Empty_unique_identifier_is_not_an_acceptable_document_number()
        =>Assert.Throws<OrderEntryException>(()=>LegacyProductionDocumentNumber.Create("SPG",Guid.Empty));
    private static string CanonicalBits(Guid id)=>string.Concat(Convert.FromHexString(id.ToString("N")).Select(b=>Convert.ToString(b,2).PadLeft(8,'0')));
    private static string DecodeBits(string value)
    {
        const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        Assert.Equal(26,value.Length);Assert.All(value,c=>Assert.Contains(c,alphabet));
        var bits=string.Concat(value.Select(c=>Convert.ToString(alphabet.IndexOf(c),2).PadLeft(5,'0')));
        Assert.Equal("00",bits[128..]);return bits[..128];
    }
}
