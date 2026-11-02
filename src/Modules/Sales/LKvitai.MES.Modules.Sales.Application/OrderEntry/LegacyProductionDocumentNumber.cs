using System.Text;
using System.Text.RegularExpressions;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.OrderEntry;
public static class LegacyProductionDocumentNumber
{
    // The table column is nvarchar(64), but the downstream write-off procedure
    // accepts nvarchar(32). Preserve every GUID bit within the narrower contract.
    public const int MaximumCharacters=32;
    public static string Create(string prefix,Guid uniqueId)
    {
        if(string.IsNullOrWhiteSpace(prefix)||!Regex.IsMatch(prefix,@"\A[A-Za-z0-9]{1,5}\z",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))||uniqueId==Guid.Empty)
            throw new OrderEntryException("CONFIRMATION_CONFIGURATION","Configure a verified one-to-five-character Sales document prefix and unique identifier.",503);
        const string alphabet="ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var encoded=new StringBuilder(26);var buffer=0;var bits=0;
        foreach(var octet in Convert.FromHexString(uniqueId.ToString("N")))
        {
            buffer=(buffer<<8)|octet;bits+=8;
            while(bits>=5){bits-=5;encoded.Append(alphabet[(buffer>>bits)&31]);}
            buffer&=(1<<bits)-1;
        }
        if(bits>0)encoded.Append(alphabet[(buffer<<(5-bits))&31]);
        var value=prefix.ToUpperInvariant()+"-"+encoded;
        if(value.Length>MaximumCharacters)throw new OrderEntryException("DOCUMENT_NUMBER_LENGTH","Production document number exceeds the verified downstream legacy contract.",503);
        return value;
    }
}
