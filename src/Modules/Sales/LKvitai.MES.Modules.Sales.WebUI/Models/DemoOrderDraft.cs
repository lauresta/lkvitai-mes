using System.Globalization;
using System.Text.RegularExpressions;

namespace LKvitai.MES.Modules.Sales.WebUI.Models;

/// <summary>Only lives in the open Blazor component. Nothing is posted to the API.</summary>
public sealed class DemoOrderDraft
{
    public string? CancellationUnavailableReason { get; set; }
    public bool PaymentsEnabled { get; set; }
    public bool ConfirmationEnabled { get; set; }
    public IReadOnlyList<int> AllowedPaymentMethods { get; set; } = [];
    public long? LegacyId { get; set; }
    public string Revision { get; set; } = "";
    public int SeriesId { get; set; }
    public int? CustomerId { get; set; }
    public int? StoreId { get; set; }
    public int? ManagerId { get; set; }
    public int? ConsultantId { get; set; }
    public int PriorityId { get; set; } = 2;
    public int? StreetId { get; set; }
    public int? StateId { get; set; }
    public bool Cancelled { get; set; }
    public bool CanEdit { get; set; } = true;
    public bool WritesAllowed { get; set; } = true;
    public decimal? ServerTotal { get; set; }
    public decimal? ServerPaid { get; set; }
    public decimal? ServerCalculated { get; set; }
    public decimal? ServerDiscountable { get; set; }
    public decimal? ServerDiscountAmount { get; set; }
    public string StateName { get; set; } = "Įvestas";
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
    public IReadOnlyList<LKvitai.MES.Modules.Sales.Contracts.Orders.OrderNoteHistoryEntry> NoteHistory { get; set; } = [];
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

    public decimal BeforeDiscount => ServerCalculated ?? Lines.Sum(x => x.Total);
    public decimal DiscountableAmount => ServerDiscountable ?? Lines.Where(x => x.Discountable).Sum(x => x.Total);
    public decimal DiscountAmount => ServerDiscountAmount ?? Math.Round(DiscountableAmount * DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
    public decimal Total => ServerTotal ?? BeforeDiscount - DiscountAmount;
    public decimal Paid => ServerPaid ?? Payments.Sum(x => x.Amount);
    public decimal Debt => Total - Paid;
}

public sealed class DemoOrderLine
{
    public bool? IsManufactured { get; set; }
    public bool HasStoredPrice { get; set; }
    public bool HasStoredDimensions { get; set; }
    public long? LegacyId { get; set; }
    public int? AccessoryId { get; set; }
    public bool CanEdit { get; set; } = true;
    public bool CanDelete { get; set; } = true;
    public string StateName { get; set; } = "ĮVESTAS";
    public int? StateId { get; set; }
    public bool UseListPrice { get; set; }
    public bool RecalculateParameters { get; set; }
    public string? CalculationRevision { get; set; }
    public string? CalculationContext { get; set; }
    public decimal? ServerAmount { get; set; }
    public decimal? ServerAccessoryAmount { get; set; }
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
    public decimal Total => (ServerAmount ?? Quantity * Price) + (ServerAccessoryAmount ?? Accessories.Sum(x => x.Quantity * x.Price));
}

public sealed class DemoResponsibleEmployee
{
    public int? DutyId { get; set; }
    public int? EmployeeId { get; set; }
    public string Duty { get; set; } = "Matuotojas";
    public string Name { get; set; } = "";
    public DateOnly Date { get; set; }
}

public sealed class DemoPayment
{
    public long? LegacyId { get; set; }
    public bool HasDate { get; set; } = true;
    public int? CurrencyId { get; set; }
    public string CurrencyCode { get; set; } = "";
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
        error = LKvitai.MES.Modules.Sales.WebUI.Resources.SalesResourceText.Localizer["Errors.Client.NumberExample"].Value;
        var text = input?.Trim() ?? "";
        var known = catalog.Series.OrderByDescending(x => x.Code.Length).FirstOrDefault(x => text.StartsWith(x.Code + "-", StringComparison.OrdinalIgnoreCase));
        if (known is null) { error = LKvitai.MES.Modules.Sales.WebUI.Resources.SalesResourceText.Localizer["Errors.Client.ExistingSeries"].Value; return false; }
        series = known.Code;
        var match = Pattern.Match("S-" + text[(known.Code.Length + 1)..]);
        if (!match.Success) return false;
        if (!int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var sequence)
            || sequence <= 0)
        {
            error = LKvitai.MES.Modules.Sales.WebUI.Resources.SalesResourceText.Localizer["Errors.Client.SequenceRange"].Value;
            return false;
        }

        var yearPart = (year % 100).ToString("D2", CultureInfo.InvariantCulture);
        var suffix = match.Groups[3].Value.ToUpperInvariant();
        if (suffix.StartsWith(yearPart, StringComparison.Ordinal)) suffix = suffix[2..];
        if (suffix.Length > 2)
        {
            error = LKvitai.MES.Modules.Sales.WebUI.Resources.SalesResourceText.Localizer["Errors.Client.YearSuffixLength"].Value;
            return false;
        }

        canonical = $"{series}-{sequence.ToString(CultureInfo.InvariantCulture)}-{yearPart}{suffix}";
        error = "";
        return true;
    }
}
