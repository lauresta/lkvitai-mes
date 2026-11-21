namespace LKvitai.MES.Modules.Sales.Infrastructure.Sql;
public sealed class SalesOrderEntryOptions
{
    public bool Enabled { get; set; }
    public Guid ExpectedDatabaseGuid { get; set; }
    public int[] AllowedSeriesIds { get; set; } = [];
    /// <summary>
    /// false (default): saving is limited to the legacy test branch (<see cref="TestBranchId"/>,
    /// TESTAVIMAS) and to <see cref="AllowedSeriesIds"/>. true: saving is allowed in every branch
    /// and series the signed-in user's own legacy permissions cover - production only.
    /// </summary>
    public bool AllBranches { get; set; }
    public const int TestBranchId = 7;
    public bool ScopeConfigured => AllBranches || AllowedSeriesIds.Length > 0;
    public bool InScope(int? branchId, int seriesId)
        => AllBranches || (branchId == TestBranchId && AllowedSeriesIds.Contains(seriesId));
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
