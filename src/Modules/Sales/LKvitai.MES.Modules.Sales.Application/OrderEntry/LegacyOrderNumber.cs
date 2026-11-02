using System.Globalization;
using System.Text.RegularExpressions;
using LKvitai.MES.Modules.Sales.Contracts.OrderEntry;
namespace LKvitai.MES.Modules.Sales.Application.OrderEntry;
public sealed record LegacyOrderNumber(int Sequence,string Group,string Canonical)
{
    public static LegacyOrderNumber Normalize(string input,string knownSeries,int currentYear)
    {
        if(string.IsNullOrWhiteSpace(input)||string.IsNullOrWhiteSpace(knownSeries))throw new OrderEntryException("INVALID_NUMBER","Order number and live series are required.");
        var prefix=knownSeries.Trim().ToUpperInvariant()+"-";
        var text=input.Trim().ToUpperInvariant();
        if(!text.StartsWith(prefix,StringComparison.Ordinal))throw new OrderEntryException("NUMBER_SERIES","Number must use the selected live order series.");
        var match=Regex.Match(text[prefix.Length..],@"^([0-9]+)(?:-([A-Z0-9]{1,4}))?$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100));
        if(!match.Success || !int.TryParse(match.Groups[1].Value,NumberStyles.None,CultureInfo.InvariantCulture,out var sequence)||sequence<=0)
            throw new OrderEntryException("INVALID_NUMBER","Enter a positive order number with an optional group suffix.");
        var year=(currentYear%100).ToString("D2",CultureInfo.InvariantCulture);
        var suffix=match.Groups[2].Value;
        if(suffix.StartsWith(year,StringComparison.Ordinal))suffix=suffix[2..];
        if(suffix.Length>2)throw new OrderEntryException("INVALID_GROUP","The current-year suffix permits at most two additional characters.");
        var group=year+suffix;
        return new LegacyOrderNumber(sequence,group,prefix+sequence.ToString(CultureInfo.InvariantCulture)+"-"+group);
    }
}
