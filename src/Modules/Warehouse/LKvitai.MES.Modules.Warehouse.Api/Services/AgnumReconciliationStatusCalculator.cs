namespace LKvitai.MES.Modules.Warehouse.Api.Services;

public static class AgnumReconciliationStatusCalculator
{
    private const decimal DeltaTolerance = 0.0001m;

    public static string GetStatus(string? sku, decimal delta)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return "NotLinked";
        }

        if (Math.Abs(delta) <= DeltaTolerance)
        {
            return "Matched";
        }

        return delta > 0m ? "Over" : "Under";
    }
}
