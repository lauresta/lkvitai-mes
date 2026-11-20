using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace LKvitai.MES.Modules.Warehouse.Api.Services.OpeningBalances;

/// <summary>One parsed CSV row of an opening balance file (blueprint 4.10).</summary>
public sealed record OpeningCsvRow(int RowNumber, string Sku, string Location, decimal? Qty, decimal? ValueEur, List<string> Errors);

/// <summary>
/// Opening balance CSV: header <c>sku,location,qty,value_eur</c> (comma or semicolon; with a semicolon the decimal
/// comma is accepted). Error codes are stable strings that the UI translates.
/// </summary>
public static class OpeningBalanceCsv
{
    public const string ErrInvalidNumber = "InvalidNumber";
    public const string ErrNegativeQty = "NegativeQty";
    public const string ErrNegativeValue = "NegativeValue";
    public const string ErrMissingSku = "MissingSku";
    public const string ErrMissingLocation = "MissingLocation";

    public static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    /// <summary>Returns the rows, or a file-level error code (EmptyFile, MissingColumns) with no rows.</summary>
    public static (IReadOnlyList<OpeningCsvRow> Rows, string? FileError) Parse(byte[] bytes)
    {
        var text = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        var headerIndex = lines.FindIndex(l => !string.IsNullOrWhiteSpace(l));
        if (headerIndex < 0)
        {
            return ([], "EmptyFile");
        }

        var delimiter = lines[headerIndex].Contains(';') ? ';' : ',';
        var header = Split(lines[headerIndex], delimiter).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Col(string name) => header.IndexOf(name);
        var iSku = Col("sku");
        var iLoc = Col("location");
        var iQty = Col("qty");
        var iVal = Col("value_eur");
        if (iSku < 0 || iLoc < 0 || iQty < 0 || iVal < 0)
        {
            return ([], "MissingColumns");
        }

        var rows = new List<OpeningCsvRow>();
        for (var i = headerIndex + 1; i < lines.Count; i++)
        {
            if (string.IsNullOrWhiteSpace(lines[i]))
            {
                continue;
            }

            var cells = Split(lines[i], delimiter);
            string Cell(int idx) => idx < cells.Count ? cells[idx].Trim() : string.Empty;
            var errors = new List<string>();
            var sku = Cell(iSku);
            var loc = Cell(iLoc);
            if (sku.Length == 0) errors.Add(ErrMissingSku);
            if (loc.Length == 0) errors.Add(ErrMissingLocation);

            var qty = Number(Cell(iQty), delimiter, errors);
            var val = Number(Cell(iVal), delimiter, errors);
            if (qty is < 0m) errors.Add(ErrNegativeQty);
            if (val is < 0m) errors.Add(ErrNegativeValue);
            rows.Add(new OpeningCsvRow(i + 1, sku, loc, qty, val, errors));
        }

        return (rows, rows.Count == 0 ? "EmptyFile" : null);
    }

    private static decimal? Number(string raw, char delimiter, List<string> errors)
    {
        var s = delimiter == ';' ? raw.Replace(',', '.') : raw;
        if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var v))
        {
            return v;
        }

        if (!errors.Contains(ErrInvalidNumber)) errors.Add(ErrInvalidNumber);
        return null;
    }

    private static List<string> Split(string line, char delimiter)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        var quoted = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                if (quoted && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else quoted = !quoted;
            }
            else if (c == delimiter && !quoted) { result.Add(sb.ToString()); sb.Clear(); }
            else sb.Append(c);
        }

        result.Add(sb.ToString());
        return result;
    }
}
