using System.Globalization;
using System.Linq.Expressions;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Domain.Purchasing;

/// <summary>One validation message of the Intrastat item fields, tied to the field (column) it concerns.</summary>
public sealed record IntrastatFieldProblem(string Field, string Message);

/// <summary>
/// Rules for the Intrastat data held on an item (commodity code, origin country, supplementary unit and factor),
/// shared by the Excel import and the bulk update so both apply the same checks.
/// </summary>
public static class IntrastatItemRules
{
    /// <summary>Length of the customs measurement unit classifier code used for the supplementary unit.</summary>
    public const int SupplementaryUnitLength = 3;

    /// <summary>Stock items missing a commodity code, a net weight or an origin country (services are not declared).</summary>
    public static readonly Expression<Func<Item, bool>> MissingData = i =>
        i.ItemType == ItemType.Stock
        && (i.CommodityCode == null || i.CommodityCode == ""
            || i.NetWeightKg == null || i.NetWeightKg <= 0m
            || i.OriginCountry == null || i.OriginCountry == "");

    public static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static bool IsCommodityCode(string? code) => code is { Length: 8 } && code.All(char.IsAsciiDigit);

    /// <summary>Parses a number typed in a spreadsheet: dot or comma as decimal separator, never the machine culture.</summary>
    public static bool TryParseDecimal(string? text, out decimal value)
    {
        value = 0m;
        var s = text?.Trim().Replace(',', '.');
        return !string.IsNullOrEmpty(s)
            && decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    /// <summary>
    /// Checks the given values; a null or blank value means "not provided" and is not checked. The supplementary unit
    /// and its factor must be provided together.
    /// </summary>
    public static IReadOnlyList<IntrastatFieldProblem> Validate(
        string? commodityCode, string? originCountry, string? supplementaryUnit, decimal? supplementaryUnitFactor)
    {
        var problems = new List<IntrastatFieldProblem>();
        var code = Normalize(commodityCode);
        if (code is not null && !IsCommodityCode(code))
            problems.Add(new("CommodityCode", "Commodity code must be exactly 8 digits."));

        var country = Normalize(originCountry);
        if (country is not null && !IntrastatCodes.IsCountryCode(country))
            problems.Add(new("OriginCountry", "Origin country must be a 2-letter ISO code."));

        problems.AddRange(ValidateUnit(Normalize(supplementaryUnit), supplementaryUnitFactor));
        return problems;
    }

    private static IEnumerable<IntrastatFieldProblem> ValidateUnit(string? unit, decimal? factor)
    {
        if (unit is not null && unit.Length != SupplementaryUnitLength)
            yield return new("SupplementaryUnit", "Supplementary unit must be exactly 3 characters.");
        if (unit is not null && factor is not > 0m)
            yield return new("SupplementaryUnitFactor", "Supplementary unit factor must be greater than zero and set together with the unit.");
        if (unit is null && factor is not null)
            yield return new("SupplementaryUnit", "Supplementary unit is required when a factor is given.");
    }

    /// <summary>
    /// Applies the provided (non-blank) values to the item and leaves everything else untouched.
    /// Returns true when the item actually changed. Values are expected to be validated.
    /// </summary>
    public static bool Apply(
        Item item, string? commodityCode, string? originCountry, string? supplementaryUnit, decimal? supplementaryUnitFactor)
    {
        var changed = false;
        var code = Normalize(commodityCode);
        if (code is not null && !string.Equals(item.CommodityCode, code, StringComparison.Ordinal))
        {
            item.CommodityCode = code;
            changed = true;
        }

        var country = Normalize(originCountry)?.ToUpperInvariant();
        if (country is not null && !string.Equals(item.OriginCountry, country, StringComparison.Ordinal))
        {
            item.OriginCountry = country;
            changed = true;
        }

        var unit = Normalize(supplementaryUnit);
        if (unit is not null
            && (!string.Equals(item.SupplementaryUnit, unit, StringComparison.Ordinal)
                || item.SupplementaryUnitFactor != supplementaryUnitFactor))
        {
            item.SupplementaryUnit = unit;
            item.SupplementaryUnitFactor = supplementaryUnitFactor;
            changed = true;
        }

        return changed;
    }
}
