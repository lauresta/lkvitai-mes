using System.Globalization;
using System.Text.RegularExpressions;

namespace LKvitai.MES.Modules.Sales.WebUI.Models;

/// <summary>Only lives in the open Blazor component. Nothing is posted to the API.</summary>
public sealed class DemoOrderDraft
{
    public string Number { get; set; } = "";
    public string Series { get; set; } = "";
    public string Customer { get; set; } = "";
    public string Manager { get; set; } = "";
    public string Store { get; set; } = "";
    public string Consultant { get; set; } = "";
    public DateOnly Date { get; set; }
    public DateOnly Due { get; set; }
    public DateOnly? MountingDate { get; set; }
    public string Priority { get; set; } = "Normal";
    public string Note { get; set; } = "";
    public string Region { get; set; } = "";
    public string City { get; set; } = "";
    public string Street { get; set; } = "";
    public string Building { get; set; } = "";
    public string Flat { get; set; } = "";
    public string Phone { get; set; } = "";
    public string AddressNote { get; set; } = "";
    public decimal DiscountPercent { get; set; }
    public List<DemoOrderLine> Lines { get; } = [];
    public List<DemoPayment> Payments { get; } = [];
    public List<DemoResponsibleEmployee> Employees { get; } = [];

    public decimal BeforeDiscount => Lines.Sum(x => x.Total);
    public decimal DiscountableAmount => Lines.Where(x => x.Discountable).Sum(x => x.Total);
    public decimal DiscountAmount => Math.Round(DiscountableAmount * DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal Total => BeforeDiscount - DiscountAmount;
    public decimal Paid => Payments.Sum(x => x.Amount);
    public decimal Debt => Total - Paid;
}

public sealed class DemoOrderLine
{
    public string Kind { get; set; } = "Product";
    public int TypeId { get; set; }
    public int CategoryId { get; set; }
    public string Category { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Quantity { get; set; } = 1;
    public string Unit { get; set; } = "pcs";
    public int WidthMm { get; set; }
    public int HeightMm { get; set; }
    public string ColourCode { get; set; } = "";
    public int? ColourId { get; set; }
    public string ControlType { get; set; } = "";
    public string Note { get; set; } = "";
    public decimal Price { get; set; }
    public bool Discountable { get; set; } = true;
    public string[] ParameterValues { get; } = new string[12];
    public bool[] ParameterManual { get; } = new bool[12];
    public List<DemoOrderLine> Accessories { get; } = [];
    public decimal Total => Quantity * Price + Accessories.Sum(x => x.Quantity * x.Price);
}

public sealed class DemoResponsibleEmployee
{
    public string Duty { get; set; } = "Matuotojas";
    public string Name { get; set; } = "";
    public DateOnly Date { get; set; }
}

public sealed class DemoPayment
{
    public DateOnly Date { get; set; }
    public string Method { get; set; } = "Bank transfer";
    public string Reference { get; set; } = "";
    public string ReceiverOrRegister { get; set; } = "";
    public decimal Amount { get; set; }
    public string Note { get; set; } = "";
}

public static class DemoOrderNumber
{
    private static readonly Regex Pattern = new(
        @"^([\p{L}\p{N}]+)-(\d+)(?:-([\p{L}\p{N}]{1,4}))?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryNormalize(string? input, int year, DemoCatalog catalog,
        out string canonical, out string series, out string error)
    {
        canonical = "";
        series = "";
        error = "Enter a number such as VV-123 or VV-123-A.";
        var match = Pattern.Match(input?.Trim() ?? "");
        if (!match.Success) return false;

        series = match.Groups[1].Value.ToUpperInvariant();
        var requestedSeries = series;
        if (!catalog.Series.Any(x => string.Equals(x.Code, requestedSeries, StringComparison.OrdinalIgnoreCase)))
        {
            error = $"Series {series} does not exist in the reference snapshot.";
            return false;
        }
        if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            || sequence <= 0)
        {
            error = "The sequence must be a positive number within the legacy range.";
            return false;
        }

        var yearPart = (year % 100).ToString("D2", CultureInfo.InvariantCulture);
        var suffix = match.Groups[3].Value.ToUpperInvariant();
        if (suffix.StartsWith(yearPart, StringComparison.Ordinal)) suffix = suffix[2..];
        if (suffix.Length > 2)
        {
            error = "Use at most two characters after the year group.";
            return false;
        }

        canonical = $"{series}-{sequence.ToString(CultureInfo.InvariantCulture)}-{yearPart}{suffix}";
        error = "";
        return true;
    }
}
