namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed class SalesOrderEntryOptions
{
    public bool Enabled { get; set; }
    public Guid ExpectedDatabaseGuid { get; set; }
    public int[] AllowedSeriesIds { get; set; } = [];
    public bool CancellationEnabled { get; set; }
    public bool PaymentsEnabled { get; set; }
    public int? PaymentCurrencyId { get; set; }
    public int[] AllowedPaymentEmployeeIds { get; set; } = [];
    public int[] AllowedPaymentMethodIds { get; set; } = [];
    public int[] AllowedRegisterIds { get; set; } = [];
    public int[] AllowedBankTransferIds { get; set; } = [];
    public bool ProductionConfirmationEnabled { get; set; }
    public string? ProductionDocumentPrefix { get; set; }
    public string BuildVersion { get; set; } = "working-tree";
}
