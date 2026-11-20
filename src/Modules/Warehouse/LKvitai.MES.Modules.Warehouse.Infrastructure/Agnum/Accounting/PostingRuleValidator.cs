using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

/// <summary>
/// Blueprint 4.6 save-time check: the chosen scheme must have a row debiting the profile's stock account;
/// for Domestic a row debiting the VAT account; for every additional-cost ExpenseAccount a row debiting it.
/// </summary>
public static class PostingRuleValidator
{
    public static IReadOnlyList<string> FindMissingRows(
        AgnumScheme? scheme,
        StockAccountProfile profile,
        VatTreatment treatment,
        string? vatAccount,
        IEnumerable<string> expenseAccounts)
    {
        var missing = new List<string>();
        if (scheme is null)
        {
            missing.Add("Scheme was not found in Agnum.");
            return missing;
        }

        var debits = (scheme.Rows ?? Array.Empty<AgnumSchemeRow>())
            .Select(r => r.Debit?.Trim())
            .Where(d => !string.IsNullOrEmpty(d))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!debits.Contains(profile.StockAccount.Trim()))
            missing.Add($"No row debiting stock account {profile.StockAccount}.");

        if (treatment == VatTreatment.Domestic)
        {
            if (string.IsNullOrWhiteSpace(vatAccount))
                missing.Add("VAT account is not configured (Agnum:Accounting:VatAccount).");
            else if (!debits.Contains(vatAccount.Trim()))
                missing.Add($"No row debiting VAT account {vatAccount}.");
        }

        foreach (var account in expenseAccounts
                     .Where(a => !string.IsNullOrWhiteSpace(a))
                     .Select(a => a.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!debits.Contains(account))
                missing.Add($"No row debiting expense account {account}.");
        }

        return missing;
    }
}
