using System.Globalization;

namespace LKvitai.MES.Modules.Sales.WebUI.Models;

/// <summary>
/// UI preview evaluator for the arithmetic subset of legacy NPP formulas.
/// It never executes SQL, VBA or arbitrary expression text. Unsupported formulas
/// remain visible and their PPP value can be entered by the operator.
/// </summary>
public static class DemoParameterFormula
{
    public static bool TryEvaluate(string? formula, DemoOrderLine line, out string value)
    {
        value = "";
        if (string.IsNullOrWhiteSpace(formula)) return false;
        try
        {
            var parser = new Parser(formula, line);
            var result = parser.Parse();
            value = result.ToString("0.############################", CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or DivideByZeroException)
        {
            return false;
        }
    }

    private sealed class Parser(string source, DemoOrderLine line)
    {
        private int _at;

        public decimal Parse()
        {
            var result = Expression();
            SkipSpace();
            if (_at != source.Length) throw new FormatException("Unsupported formula syntax.");
            return result;
        }

        private decimal Expression()
        {
            var result = Term();
            while (true)
            {
                SkipSpace();
                if (Take('+')) result += Term();
                else if (Take('-')) result -= Term();
                else return result;
            }
        }

        private decimal Term()
        {
            var result = Factor();
            while (true)
            {
                SkipSpace();
                if (Take('*')) result *= Factor();
                else if (Take('/')) result /= Factor();
                else return result;
            }
        }

        private decimal Factor()
        {
            SkipSpace();
            if (Take('+')) return Factor();
            if (Take('-')) return -Factor();
            if (Take('('))
            {
                var result = Expression();
                SkipSpace();
                if (!Take(')')) throw new FormatException("Missing parenthesis.");
                return result;
            }
            if (_at >= source.Length) throw new FormatException("Missing operand.");
            if (char.IsAsciiLetter(source[_at]))
            {
                var start = _at;
                while (_at < source.Length && (char.IsAsciiLetterOrDigit(source[_at]) || source[_at] == '_')) _at++;
                var name = source[start.._at];
                if (name.Equals("Width", StringComparison.OrdinalIgnoreCase)) return line.WidthMm;
                if (name.Equals("Height", StringComparison.OrdinalIgnoreCase)) return line.HeightMm;
                if (name.Equals("Quantity", StringComparison.OrdinalIgnoreCase)) return line.Quantity;
                if (name.Equals("Round", StringComparison.OrdinalIgnoreCase))
                {
                    SkipSpace();
                    if (!Take('(')) throw new FormatException("Expected Round arguments.");
                    var amount = Expression();
                    SkipSpace();
                    if (!Take(',')) throw new FormatException("Expected Round precision.");
                    var precision = Expression();
                    SkipSpace();
                    if (!Take(')') || precision != decimal.Truncate(precision) || precision is < 0 or > 8)
                        throw new FormatException("Unsupported Round precision.");
                    return Math.Round(amount, (int)precision, MidpointRounding.ToEven);
                }
                throw new FormatException("Unsupported function or variable.");
            }
            var numberStart = _at;
            while (_at < source.Length && (char.IsAsciiDigit(source[_at]) || source[_at] == '.')) _at++;
            if (numberStart == _at || !decimal.TryParse(source[numberStart.._at], NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out var number)) throw new FormatException("Invalid number.");
            return number;
        }

        private bool Take(char value)
        {
            if (_at >= source.Length || source[_at] != value) return false;
            _at++;
            return true;
        }

        private void SkipSpace()
        {
            while (_at < source.Length && char.IsWhiteSpace(source[_at])) _at++;
        }
    }
}
