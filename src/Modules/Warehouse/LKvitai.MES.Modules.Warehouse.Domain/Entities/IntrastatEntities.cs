namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

/// <summary>Singleton row (Id = 1): the VAT payer that files the Intrastat declaration.</summary>
public sealed class IntrastatDeclarant
{
    public const int SingletonId = 1;

    public int Id { get; set; } = SingletonId;
    /// <summary>VAT payer code with the country letters, up to 14 characters (for example LT123456789).</summary>
    public string? VatCode { get; set; }
    public string? Title { get; set; }
    public string? Street { get; set; }
    public string? ZipCode { get; set; }
    /// <summary>Person who prepares the declaration.</summary>
    public string? ContactPerson { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
}

/// <summary>One generated Intrastat arrivals XML file (a version of the declaration of a month).</summary>
public sealed class IntrastatXmlExport
{
    public int Id { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    /// <summary>1, 2, ... per month; a correction is the next version.</summary>
    public int Version { get; set; }
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public string GeneratedBy { get; set; } = string.Empty;
    public bool IsZeroReport { get; set; }
    public int RowCount { get; set; }
    public decimal TotalValueEur { get; set; }
    public decimal TotalNetMassKg { get; set; }
    /// <summary>SHA-256 of the UTF-8 bytes of <see cref="Xml"/>, lower-case hex.</summary>
    public string Sha256 { get; set; } = string.Empty;
    public string Xml { get; set; } = string.Empty;
    /// <summary>Set by hand when the file was uploaded to the customs portal.</summary>
    public DateTimeOffset? FiledAt { get; set; }
    public string? FiledBy { get; set; }
}
