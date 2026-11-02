using System.Globalization;

namespace LKvitai.MES.Modules.Sales.Application.OrderEntry;

public sealed class LegacyProductRuleException(string code, string field, string message) : Exception(message)
{
    public string Code { get; } = code;
    public string Field { get; } = field;
}

public sealed record LegacyStockPreviewDecision(bool? IsShort,string Disposition);

/// <summary>Server allowlist for current legacy NPP syntax. No Eval, SQL or browser-defined functions.</summary>
public static class LegacyProductCalculator
{
    /// <summary>
    /// Checks grammar and the function/token allowlist without evaluating a
    /// sample product. Input-specific division, constants and rounding errors
    /// belong to configuration validation, not type capability discovery.
    /// </summary>
    public static string? FindUnsupportedReason(IReadOnlyList<string?> formulas)
    {
        if(formulas.Count!=12)return "Exactly twelve PPP formula slots are required.";
        for(var i=0;i<formulas.Count;i++)
        {
            if(formulas[i] is null)continue;
            if(IsNeraLiteral(formulas[i]!))
            {
                if(i<11)continue;
                return "PPP12: The nera text literal is not valid for numeric material consumption.";
            }
            try { new Parser(formulas[i]!,0,0,0,null,null,new Dictionary<string,double>(),true).Parse(); }
            catch(FormatException ex) { return $"PPP{i+1:D2}: {ex.Message}"; }
        }
        return null;
    }

    public static string?[] Calculate(int widthMm, int heightMm, decimal quantity, int? colourId, string? control,
        IReadOnlyList<string?> formulas, IReadOnlyList<string?> enteredValues,
        IReadOnlyDictionary<string,double> constants, bool recalculate)
    {
        if(formulas is null || enteredValues is null || formulas.Count!=12 || enteredValues.Count!=12)
            throw new LegacyProductRuleException("PPP_SHAPE","parameters","Exactly twelve PPP slots are required.");
        var result=enteredValues.ToArray();
        for(var i=0;i<12;i++)
        {
            try
            {
                if(recalculate && formulas[i] is not null)
                {
                    if(IsNeraLiteral(formulas[i]!))
                    {
                        if(i==11)throw new FormatException("The nera text literal is not valid for numeric material consumption.");
                        result[i]="nera";
                    }
                    else
                    {
                        var value=new Parser(formulas[i]!,widthMm,heightMm,quantity,colourId,control,constants).Parse();
                        if(!double.IsFinite(value)) throw new FormatException("The formula did not produce a finite value.");
                        result[i]=value.ToString("G15",CultureInfo.InvariantCulture);
                    }
                }
                if(i<11)
                {
                    if(result[i]?.Length>15) throw new FormatException("The PPP value exceeds fifteen characters.");
                }
                else if(result[i] is not null)
                {
                    if(string.IsNullOrWhiteSpace(result[i])) { result[i]=null; continue; }
                    if(!decimal.TryParse(result[i],NumberStyles.Float,CultureInfo.InvariantCulture,out var value))
                        throw new FormatException("PPP12 must be a decimal number.");
                    // SQL decimal(18,3) conversion rounds, rather than truncating to three decimal places.
                    value=Math.Round(value,3,MidpointRounding.AwayFromZero);
                    if(Math.Abs(value)>999999999999999.999m) throw new OverflowException("PPP12 exceeds legacy storage precision.");
                    result[i]=value.ToString("0.###",CultureInfo.InvariantCulture);
                }
            }
            catch(Exception ex) when(ex is FormatException or OverflowException or DivideByZeroException)
            { throw new LegacyProductRuleException("PPP_CALCULATION_FAILED",$"PPP{i+1:D2}",ex.Message); }
        }
        return result;
    }

    // Owner-confirmed sentinel in the existing SQL formulas, not an identifier evaluator.
    private static bool IsNeraLiteral(string formula)=>string.Equals(formula.Trim(),"Nera",StringComparison.OrdinalIgnoreCase);

    public static decimal SuggestAccessoryQuantity(int? unitId,int widthMm,int heightMm,decimal quantity) => unitId switch
    {
        -1789163110 => widthMm/1000m*quantity,
        1046262104 => heightMm/1000m*quantity,
        1664584723 => quantity,
        -1859919715 => widthMm/1000m*heightMm/1000m*quantity,
        _ => quantity, // Non-formula units require operator quantity; this is a UI suggestion only.
    };

    /// <summary>Fresh pre-mutation stock plus this line's existing same-component reservation.</summary>
    public static string? CheckStock(decimal freeQuantity,decimal ownReservedQuantity,decimal requiredQuantity,int? actionId,string? unit=null)
    {
        if(requiredQuantity<0 || ownReservedQuantity<0)
            throw new LegacyProductRuleException("INVALID_MATERIAL_REQUIREMENT","PPP12","Material requirement must be nonnegative.");
        if(freeQuantity+ownReservedQuantity-requiredQuantity>0) return null;
        if(actionId is null)
            throw new LegacyProductRuleException("UNKNOWN_STOCK_POLICY","colourId","The material shortage action is not configured.");
        var available=freeQuantity+ownReservedQuantity;
        var suffix=string.IsNullOrWhiteSpace(unit)?"":" "+unit.Trim();
        var quantities=$"Required {requiredQuantity.ToString("0.###",CultureInfo.InvariantCulture)}{suffix}; available {available.ToString("0.###",CultureInfo.InvariantCulture)}{suffix}.";
        var equality=available==requiredQuantity ? " The legacy rule requires availability greater than the required quantity." : "";
        return actionId switch
        {
            1=>null,
            2=>$"Material shortage. {quantities}{equality} Entry is allowed with a warning; confirm availability with the responsible employee.",
            3=>throw new LegacyProductRuleException("INSUFFICIENT_MATERIAL","colourId",$"Product not saved: insufficient material. {quantities}{equality} Choose another material or reduce the quantity or dimensions."),
            _=>null, // VBA Select Case has no matching action for other numeric values.
        };
    }

    /// <summary>Display-only description. It never throws a shortage or authorizes a mutation.</summary>
    public static LegacyStockPreviewDecision PreviewStock(decimal freeQuantity,decimal ownReservedQuantity,decimal? requiredQuantity,int? actionId)
    {
        if(requiredQuantity is null)return new(null,"not-calculated");
        if(requiredQuantity<0||ownReservedQuantity<0)return new(null,"invalid-requirement");
        if(freeQuantity+ownReservedQuantity-requiredQuantity.Value>0)return new(false,"enough");
        return new(true,actionId switch { null=>"unknown-policy",2=>"warn",3=>"block",_=>"allow" });
    }

    private sealed class Parser(string source,int width,int height,decimal quantity,int? colour,string? control,IReadOnlyDictionary<string,double> constants,bool validateOnly=false)
    {
        private int _at;
        public double Parse(){var value=Expression();Skip();if(_at!=source.Length)throw new FormatException("Unsupported formula syntax.");return value;}
        private double Expression(){var value=Term();while(true){Skip();if(Take('+'))value+=Term();else if(Take('-'))value-=Term();else return value;}}
        private double Term(){var value=Factor();while(true){Skip();if(Take('*'))value*=Factor();else if(Take('/')){var divisor=Factor();if(validateOnly){value=0;continue;}if(divisor==0)throw new DivideByZeroException("Formula division by zero.");value/=divisor;}else return value;}}
        private double Factor()
        {
            Skip();if(Take('+'))return Factor();if(Take('-'))return -Factor();
            if(Take('(')){var value=Expression();Skip();if(!Take(')'))throw new FormatException("Missing formula parenthesis.");return value;}
            if(_at>=source.Length)throw new FormatException("Missing formula operand.");
            if(char.IsAsciiLetter(source[_at]))
            {
                var start=_at;while(_at<source.Length&&(char.IsAsciiLetterOrDigit(source[_at])||source[_at]=='_'))_at++;
                var name=source[start.._at];
                if(name.Equals("Width",StringComparison.OrdinalIgnoreCase))return width;
                if(name.Equals("Height",StringComparison.OrdinalIgnoreCase))return height;
                if(name.Equals("Quantity",StringComparison.OrdinalIgnoreCase))return (double)quantity;
                if(name.Equals("Colour",StringComparison.OrdinalIgnoreCase))return validateOnly?0:colour??throw new FormatException("Colour is required by this formula.");
                if(name.Equals("ControlType",StringComparison.OrdinalIgnoreCase))
                    return validateOnly?0:colour.HasValue&&double.TryParse(control,NumberStyles.Float,CultureInfo.InvariantCulture,out var numericControl)?numericControl:throw new FormatException("This formula requires a numeric control value and colour.");
                Skip();if(!Take('('))throw new FormatException($"Unsupported formula token: {name}.");
                var argument=Expression();Skip();
                if(name.Equals("Round",StringComparison.OrdinalIgnoreCase))
                {
                    if(!Take(','))throw new FormatException("Round precision is required.");
                    var precision=Expression();Skip();if(!Take(')'))throw new FormatException("Invalid Round arguments.");
                    if(validateOnly)return 0;
                    // Exact integer checks preserve VBA Round semantics; tolerances would accept invalid precision.
                    if(!precision.Equals(Math.Truncate(precision))||precision<0||precision>15)throw new FormatException("Invalid Round precision.");
                    return Math.Round(argument,(int)precision,MidpointRounding.ToEven);
                }
                if(!Take(')'))throw new FormatException("Invalid function arguments.");
                return Function(name,argument);
            }
            var first=_at;while(_at<source.Length&&(char.IsAsciiDigit(source[_at])||source[_at]=='.'))_at++;
            if(first==_at||!double.TryParse(source[first.._at],NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var number))throw new FormatException("Unsupported formula operand.");
            return number;
        }
        private double Function(string name,double value)
        {
            var key=name.ToUpperInvariant();
            var corrector=key.StartsWith("CORRECTORSQNT",StringComparison.Ordinal);
            if(!corrector&&!key.StartsWith("CARRIERQNT",StringComparison.Ordinal))throw new FormatException($"Unsupported formula function: {name}.");
            var suffix=key[(corrector?13:10)..];
            if(suffix is not("127AB" or "89AB" or "127CDE" or "89CDE"))throw new FormatException($"Unsupported formula function: {name}.");
            if(validateOnly)return 0;
            var size=suffix.StartsWith("127",StringComparison.Ordinal)?"127":"89";
            var family=suffix.EndsWith("CDE",StringComparison.Ordinal)?"CDE":"AB";
            double Constant(string n)=>constants.TryGetValue(n,out var c)&&double.IsFinite(c)?c:throw new FormatException($"Missing calculation constant: {n}.");
            var step=Constant("MaxStep"+size);var edge=Constant("EdgeFix"+size+"_"+family);
            if(step==0)throw new DivideByZeroException("Calculation step is zero.");
            var carriers=Math.Round(((value-edge)/step+1)+0.5,0,MidpointRounding.ToEven);
            if(family=="CDE"&&carriers%2==1)carriers++;
            if(!corrector)return carriers;
            // Carriers is already rounded to an integer; exact sentinel values are intentional.
            if(carriers.Equals(0d))return 0;
            if(carriers.Equals(1d))throw new DivideByZeroException("One carrier cannot produce a corrector spacing.");
            return Math.Round(((step-(value-edge)/(carriers-1))*carriers)/6,0,MidpointRounding.ToEven);
        }
        private void Skip(){while(_at<source.Length&&char.IsWhiteSpace(source[_at]))_at++;}
        private bool Take(char c){if(_at>=source.Length||source[_at]!=c)return false;_at++;return true;}
    }
}
