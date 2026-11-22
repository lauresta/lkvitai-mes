using System.Globalization;
using System.Text;
using System.Xml;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Domain.Purchasing;

public sealed record IntrastatXmlProblem(string Code, string Text);

public sealed record IntrastatXmlResult(
    string? Xml,
    IReadOnlyList<IntrastatXmlProblem> Problems,
    IReadOnlyList<string> Notes,
    bool IsZeroReport,
    int ItemCount,
    decimal TotalValueEur,
    decimal TotalNetMassKg);

/// <summary>
/// Writes the Intrastat arrivals declaration as IDAIS XML (message INTRASTATReportSubmission): one VAT payer, trade
/// direction IMPORT, one reporting month, one version per upload.
/// <para>
/// FORMAT SOURCE: the structure, element names, order and number formats are taken from the customs filling
/// instruction "Intrastato ataskaitos XML formatu pildymo instrukcija v1.00" (2026-03-13). The schema file
/// INTRASTATReportSubmission.xsd could not be obtained (it is attachment 1 of that instruction and is not published on
/// the public customs pages), so the output is NOT validated against the XSD. In particular the XML namespace (if the
/// XSD defines one) is unknown and none is written.
/// </para>
/// </summary>
public static class IntrastatXmlWriter
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static IntrastatXmlResult Write(
        IntrastatDeclarant? declarant, int year, int month, IReadOnlyList<IntrastatRow> rows, IReadOnlyList<string> warnings)
    {
        var arrivals = rows.Where(r => !r.IsReturn).ToList();
        var returns = rows.Where(r => r.IsReturn).ToList();
        var problems = Check(declarant, arrivals, returns, warnings);
        var notes = ReturnNotes(returns);
        if (problems.Count > 0)
            return new IntrastatXmlResult(null, problems, notes, false, 0, 0m, 0m);

        var zero = IntrastatReport.IsZeroReport(rows, warnings);
        var xml = Render(declarant!, year, month, zero ? [] : arrivals, zero);
        return new IntrastatXmlResult(xml, problems, notes, zero, arrivals.Count,
            arrivals.Sum(r => r.ValueEur), arrivals.Sum(r => r.NetMassKg));
    }

    public static bool IsDeclarantComplete(IntrastatDeclarant? d)
        => d is not null && !string.IsNullOrWhiteSpace(d.VatCode) && !string.IsNullOrWhiteSpace(d.Title);

    /// <summary>VAT payer code: two country letters followed by up to 12 letters or digits (14 characters at most).</summary>
    public static bool IsVatCode(string? code)
        => code is { Length: >= 3 and <= 14 } && char.IsAsciiLetter(code[0]) && char.IsAsciiLetter(code[1])
           && code.All(char.IsAsciiLetterOrDigit);

    private static List<IntrastatXmlProblem> Check(
        IntrastatDeclarant? declarant, IReadOnlyList<IntrastatRow> arrivals, IReadOnlyList<IntrastatRow> returns,
        IReadOnlyList<string> warnings)
    {
        var problems = new List<IntrastatXmlProblem>();
        if (!IsDeclarantComplete(declarant))
            problems.Add(new("DeclarantMissing", "The declarant (VAT payer code and name) is not set. Fill it in on the Intrastat page."));
        else if (!IsVatCode(declarant!.VatCode!.Trim()))
            problems.Add(new("DeclarantVatInvalid", "The declarant VAT code must start with two country letters (up to 14 characters)."));
        if (warnings.Count > 0)
            problems.Add(new("IncompleteLines", $"{warnings.Count} lines are left out of the report because data is missing; the declaration would be incomplete."));
        if (arrivals.Any(r => r.SupplementaryUnit is { Length: not 3 }))
            problems.Add(new("SupplementaryUnitCode", "The supplementary unit of an item must be the 3-character code of the customs measurement unit classifier."));
        if (arrivals.Count == 0 && returns.Count > 0)
            problems.Add(new("OnlyReturns", "The month has only returns to suppliers and no arrivals; decide with the accountant how to declare it."));
        return problems;
    }

    private static List<string> ReturnNotes(IReadOnlyList<IntrastatRow> returns)
        => returns.Count == 0
            ? []
            : [$"{returns.Count} return rows (nature 21, total {returns.Sum(r => r.ValueEur).ToString("0.00", Inv)} EUR) are not in the file: the filling instruction has no rule for them. Declare them with the accountant."];

    private static string Render(IntrastatDeclarant d, int year, int month, IReadOnlyList<IntrastatRow> items, bool zero)
    {
        var sw = new Utf8StringWriter();
        using (var w = XmlWriter.Create(sw, new XmlWriterSettings { Indent = true, Encoding = Encoding.UTF8 }))
        {
            w.WriteStartDocument();
            w.WriteStartElement("INTRASTATReportSubmission");
            w.WriteElementString("SubmitterType", "VAT_PAYER");
            WriteDeclarant(w, d);
            WritePreparation(w, d);
            WriteReport(w, year, month, items, zero);
            w.WriteEndElement();
            w.WriteEndDocument();
        }

        return sw.ToString();
    }

    private static void WriteDeclarant(XmlWriter w, IntrastatDeclarant d)
    {
        w.WriteStartElement("VATPayer");
        Optional(w, "Title", d.Title);
        w.WriteElementString("IdentifierVAT", d.VatCode!.Trim().ToUpperInvariant());
        if (HasAny(d.Street, d.ZipCode))
        {
            w.WriteStartElement("Address");
            Optional(w, "StreetHouseCity", d.Street);
            Optional(w, "ZipCode", d.ZipCode);
            w.WriteEndElement();
        }

        WriteContact(w, d.Email, d.Phone);
        w.WriteEndElement();
    }

    private static void WritePreparation(XmlWriter w, IntrastatDeclarant d)
    {
        if (string.IsNullOrWhiteSpace(d.ContactPerson)) return;
        w.WriteStartElement("ReportPreparation");
        w.WriteStartElement("ResponsiblePerson");
        w.WriteElementString("FirstLastName", d.ContactPerson.Trim());
        WriteContact(w, d.Email, d.Phone);
        w.WriteEndElement();
        w.WriteEndElement();
    }

    private static void WriteContact(XmlWriter w, string? email, string? phone)
    {
        if (!HasAny(email, phone)) return;
        w.WriteStartElement("Contact");
        Optional(w, "Email", email);
        Optional(w, "Phone", phone);
        w.WriteEndElement();
    }

    private static void WriteReport(XmlWriter w, int year, int month, IReadOnlyList<IntrastatRow> items, bool zero)
    {
        w.WriteStartElement("INTRASTATReport");
        w.WriteElementString("ReportingYear", year.ToString(Inv));
        w.WriteElementString("ReportingMonth", month.ToString(Inv));
        w.WriteElementString("TradeDirection", "IMPORT");
        w.WriteElementString("ZeroTradeIndicator", zero ? "true" : "false");
        if (!zero)
        {
            w.WriteStartElement("INTRASTATReportItemList");
            for (var i = 0; i < items.Count; i++) WriteItem(w, i + 1, items[i]);
            w.WriteEndElement();
        }

        w.WriteEndElement();
    }

    private static void WriteItem(XmlWriter w, int number, IntrastatRow r)
    {
        w.WriteStartElement("INTRASTATReportItem");
        w.WriteElementString("ReportItemNumber", number.ToString(Inv));
        w.WriteElementString("CombinedNomenclatureCode", r.CommodityCode);
        w.WriteElementString("CountryOfDispatchOrDestinationCode", r.Country);
        w.WriteElementString("CountryOfOriginCode", r.OriginCountry);
        w.WriteElementString("NatureOfTransactionCode", r.Nature);
        w.WriteElementString("DeliveryTermsCode", r.DeliveryTerms);
        w.WriteElementString("ModeOfTransportCode", r.TransportMode);
        w.WriteElementString("NetMass", Decimal3(r.NetMassKg));
        if (r.SupplementaryUnit is not null)
        {
            w.WriteElementString("QuantityInSupplementaryUnitOfMeasure", Decimal3(r.SupplementaryQuantity ?? 0m));
            w.WriteElementString("SupplementaryUnitOfMeasureCode", r.SupplementaryUnit);
        }

        w.WriteElementString("InvoicedAmount", r.ValueEur.ToString("0.00", Inv));
        w.WriteEndElement();
    }

    /// <summary>Integer or decimal with up to three digits after the point, invariant separator.</summary>
    public static string Decimal3(decimal value) => decimal.Round(value, 3, MidpointRounding.AwayFromZero).ToString("0.###", Inv);

    private static bool HasAny(params string?[] values) => values.Any(v => !string.IsNullOrWhiteSpace(v));

    private static void Optional(XmlWriter w, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value)) w.WriteElementString(name, value.Trim());
    }

    private sealed class Utf8StringWriter : StringWriter
    {
        public override Encoding Encoding => new UTF8Encoding(false);
    }
}
