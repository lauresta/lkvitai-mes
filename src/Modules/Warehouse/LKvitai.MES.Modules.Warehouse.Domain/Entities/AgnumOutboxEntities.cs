namespace LKvitai.MES.Modules.Warehouse.Domain.Entities;

/// <summary>Blueprint 4.7 statuses of an outbox row.</summary>
public static class OutboxStatus
{
    public const string Pending = "Pending";
    public const string Sending = "Sending";
    public const string Sent = "Sent";
    public const string Unknown = "Unknown";
    public const string NeedsReview = "NeedsReview";
    public const string Annulled = "Annulled";

    /// <summary>Rows that no longer wait for anything.</summary>
    public static bool IsTerminal(string status) => status is Sent or Annulled;
}

public static class OutboxOperation
{
    public const string Post = "Post";
    public const string Annul = "Annul";
}

public static class OutboxDocumentType
{
    public const string Shipment = "InboundShipment";
    public const string CostDocument = "AdditionalCostDocument";
}

/// <summary>Blueprint 4.7 "agnum_document_outbox": one row per posting or annulment, written in the posting transaction.</summary>
public sealed class AgnumDocumentOutbox
{
    public long Id { get; set; }
    public Guid Key { get; set; }
    public int Revision { get; set; }
    public string DocumentType { get; set; } = OutboxDocumentType.Shipment;
    public int DocumentId { get; set; }
    public int SupplierId { get; set; }
    public string DocumentNumber { get; set; } = string.Empty;
    public DateOnly DocumentDate { get; set; }
    public string Currency { get; set; } = "EUR";
    public decimal GrossAmount { get; set; }
    public string Operation { get; set; } = OutboxOperation.Post;
    public string PayloadJson { get; set; } = string.Empty;
    public string PayloadHash { get; set; } = string.Empty;
    public string Status { get; set; } = OutboxStatus.Pending;
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LeasedUntil { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; } = DateTimeOffset.UtcNow;
    public long? AgnumDocumentId { get; set; }
    public long? AgnumOperationId { get; set; }
    public string? AppliedPostingsJson { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string CreatedBy { get; set; } = string.Empty;
    /// <summary>Optimistic claim token: every claim and every state change increments it, so two senders cannot both take a row.</summary>
    public int Version { get; set; }
}

/// <summary>Singleton row (Id = 1): blueprint 4.6 tab "Sending".</summary>
public sealed class AgnumSendingSettings
{
    public int Id { get; set; } = 1;
    public bool Enabled { get; set; }
    /// <summary>Documents dated before this day are never sent.</summary>
    public DateOnly? StartDate { get; set; }
    public DateTimeOffset? LastReconciledAt { get; set; }
    public int LastReconciliationFindings { get; set; }
    public string? LastReconciliationError { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? UpdatedBy { get; set; }
}

public static class ReconciliationKinds
{
    /// <summary>Sent in Warehouse, not found in Agnum.</summary>
    public const string MissingInAgnum = "MissingInAgnum";
    /// <summary>Found, but sums or postings differ from the snapshot.</summary>
    public const string Mismatch = "Mismatch";
    /// <summary>Agnum holds a Warehouse marked document that Warehouse does not consider sent.</summary>
    public const string UnexpectedInAgnum = "UnexpectedInAgnum";
    /// <summary>A stock account purchase created in Agnum outside Warehouse.</summary>
    public const string ForeignDocument = "ForeignDocument";
    /// <summary>A ManualInAgnum document that could not be found by supplier and number.</summary>
    public const string ManualNotFound = "ManualNotFound";
}

/// <summary>Result row of the nightly reconciliation (blueprint 4.7 "Сверка"); the last run replaces the previous one.</summary>
public sealed class AgnumReconciliationFinding
{
    public long Id { get; set; }
    public DateTimeOffset RunAt { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? DocumentType { get; set; }
    public int? DocumentId { get; set; }
    public Guid? Key { get; set; }
    public string? DocumentNumber { get; set; }
    public string Details { get; set; } = string.Empty;
}
