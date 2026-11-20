using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;

public sealed class AccountingOptions
{
    /// <summary>Agnum account debited by the VAT row of a Domestic scheme.</summary>
    public string? VatAccount { get; set; }
}

/// <summary>
/// Supplies ExpenseAccount values of additional_cost_types with IncludedInCost = false.
/// The table belongs to stage W2 (blueprint 4.4); until it lands the default returns nothing.
/// </summary>
public interface IExpenseAccountSource
{
    Task<IReadOnlyList<string>> GetExpenseAccountsAsync(CancellationToken ct);
}

public sealed class NoExpenseAccountSource : IExpenseAccountSource
{
    public Task<IReadOnlyList<string>> GetExpenseAccountsAsync(CancellationToken ct)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
}

public sealed class AccountingConfigValidationException : Exception
{
    public IReadOnlyList<string> Problems { get; }
    public AccountingConfigValidationException(IReadOnlyList<string> problems)
        : base("Configuration was not saved: " + string.Join(" ", problems))
        => Problems = problems;
}

public sealed class AccountingConfigService
{
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    private readonly WarehouseDbContext _db;
    private readonly IAgnumReferenceClient _agnum;
    private readonly IExpenseAccountSource _expenses;
    private readonly AccountingOptions _options;

    public AccountingConfigService(
        WarehouseDbContext db,
        IAgnumReferenceClient agnum,
        IExpenseAccountSource expenses,
        IOptions<AccountingOptions> options)
    {
        _db = db;
        _agnum = agnum;
        _expenses = expenses;
        _options = options.Value;
    }

    public async Task<StockAccountProfile> SaveProfileAsync(StockAccountProfile input, string user, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.StockAccount))
            throw new AccountingConfigValidationException(new[] { "Name and stock account are required." });

        var profile = input.Id == 0 ? null : await _db.StockAccountProfiles.FirstOrDefaultAsync(p => p.Id == input.Id, ct);
        if (input.Id != 0 && profile is null)
            throw new KeyNotFoundException($"Profile {input.Id} not found.");

        var before = profile is null ? null : Snapshot(profile);
        if (profile is null)
        {
            profile = new StockAccountProfile();
            _db.StockAccountProfiles.Add(profile);
        }

        profile.Name = input.Name.Trim();
        profile.StockAccount = input.StockAccount.Trim();
        profile.AgnumSndId = input.AgnumSndId;
        profile.PriceDifferenceAccount = Clean(input.PriceDifferenceAccount);
        profile.WriteOffAccount = Clean(input.WriteOffAccount);
        profile.MarkdownAccount = Clean(input.MarkdownAccount);
        profile.IsDefault = input.IsDefault;

        if (profile.IsDefault)
        {
            foreach (var other in await _db.StockAccountProfiles.Where(p => p.IsDefault && p.Id != profile.Id).ToListAsync(ct))
            {
                var otherBefore = Snapshot(other);
                other.IsDefault = false;
                Audit("StockAccountProfile", other.Id.ToString(), "Update", user, otherBefore, Snapshot(other));
            }
        }

        // A changed stock account must keep every existing rule of this profile valid.
        if (profile.Id != 0)
        {
            var rules = await _db.AgnumPostingRules.Where(r => r.StockAccountProfileId == profile.Id).ToListAsync(ct);
            await ValidateRulesAsync(profile, rules, ct);
        }

        await _db.SaveChangesAsync(ct); // assigns the id for new profiles
        Audit("StockAccountProfile", profile.Id.ToString(), before is null ? "Create" : "Update", user, before, Snapshot(profile));
        await _db.SaveChangesAsync(ct);
        return profile;
    }

    public async Task DeleteProfileAsync(int id, string user, CancellationToken ct)
    {
        var profile = await _db.StockAccountProfiles.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException($"Profile {id} not found.");
        if (await _db.ItemCategories.AnyAsync(c => c.StockAccountProfileId == id, ct))
            throw new AccountingConfigValidationException(new[] { "Profile is used by item categories." });

        var rules = await _db.AgnumPostingRules.Where(r => r.StockAccountProfileId == id).ToListAsync(ct);
        foreach (var rule in rules)
        {
            Audit("AgnumPostingRule", rule.Id.ToString(), "Delete", user, Snapshot(rule), null);
            _db.AgnumPostingRules.Remove(rule);
        }
        Audit("StockAccountProfile", id.ToString(), "Delete", user, Snapshot(profile), null);
        _db.StockAccountProfiles.Remove(profile);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AgnumPostingRule> SaveRuleAsync(AgnumPostingRule input, string user, CancellationToken ct)
    {
        var profile = await _db.StockAccountProfiles.FirstOrDefaultAsync(p => p.Id == input.StockAccountProfileId, ct)
            ?? throw new AccountingConfigValidationException(new[] { "Profile not found." });

        var vatCode = Clean(input.VatCode);
        if (input.VatTreatment != VatTreatment.Domestic) vatCode = null;

        var rule = await _db.AgnumPostingRules.FirstOrDefaultAsync(
            r => r.StockAccountProfileId == profile.Id && r.VatTreatment == input.VatTreatment, ct);
        var before = rule is null ? null : Snapshot(rule);
        var candidate = new AgnumPostingRule
        {
            StockAccountProfileId = profile.Id,
            VatTreatment = input.VatTreatment,
            SchemeId = input.SchemeId,
            VatCode = vatCode
        };

        await ValidateRulesAsync(profile, new[] { candidate }, ct);

        if (rule is null)
        {
            rule = candidate;
            _db.AgnumPostingRules.Add(rule);
        }
        else
        {
            rule.SchemeId = candidate.SchemeId;
            rule.VatCode = candidate.VatCode;
        }

        await _db.SaveChangesAsync(ct);
        Audit("AgnumPostingRule", rule.Id.ToString(), before is null ? "Create" : "Update", user, before, Snapshot(rule));
        await _db.SaveChangesAsync(ct);
        return rule;
    }

    public async Task DeleteRuleAsync(int id, string user, CancellationToken ct)
    {
        var rule = await _db.AgnumPostingRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new KeyNotFoundException($"Rule {id} not found.");
        Audit("AgnumPostingRule", id.ToString(), "Delete", user, Snapshot(rule), null);
        _db.AgnumPostingRules.Remove(rule);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetCategoryProfileAsync(int categoryId, int? profileId, string user, CancellationToken ct)
    {
        var category = await _db.ItemCategories.FirstOrDefaultAsync(c => c.Id == categoryId, ct)
            ?? throw new KeyNotFoundException($"Category {categoryId} not found.");
        if (profileId is not null && !await _db.StockAccountProfiles.AnyAsync(p => p.Id == profileId, ct))
            throw new AccountingConfigValidationException(new[] { "Profile not found." });

        var before = new { category.Id, category.StockAccountProfileId };
        category.StockAccountProfileId = profileId;
        Audit("ItemCategoryProfile", categoryId.ToString(), "Update", user, before, new { category.Id, category.StockAccountProfileId });
        await _db.SaveChangesAsync(ct);
    }

    /// <summary>Blueprint 4.5: own profile, else the nearest ancestor's, else the default profile.</summary>
    public static int? ResolveProfileId(
        int categoryId,
        IReadOnlyDictionary<int, (int? ParentId, int? ProfileId)> categories,
        int? defaultProfileId)
    {
        var seen = new HashSet<int>();
        int? current = categoryId;
        while (current is not null && seen.Add(current.Value) && categories.TryGetValue(current.Value, out var node))
        {
            if (node.ProfileId is not null) return node.ProfileId;
            current = node.ParentId;
        }
        return defaultProfileId;
    }

    private async Task ValidateRulesAsync(StockAccountProfile profile, IReadOnlyCollection<AgnumPostingRule> rules, CancellationToken ct)
    {
        if (rules.Count == 0) return;

        var schemes = (await _agnum.GetSchemesAsync(ct)).ToDictionary(s => s.IdSch);
        var expenseAccounts = await _expenses.GetExpenseAccountsAsync(ct);
        var problems = new List<string>();
        foreach (var rule in rules)
        {
            schemes.TryGetValue(rule.SchemeId, out var scheme);
            foreach (var p in PostingRuleValidator.FindMissingRows(scheme, profile, rule.VatTreatment, _options.VatAccount, expenseAccounts))
                problems.Add($"[{profile.Name} / {rule.VatTreatment} / scheme {rule.SchemeId}] {p}");
        }

        if (problems.Count > 0) throw new AccountingConfigValidationException(problems);
    }

    private void Audit(string entity, string entityId, string action, string user, object? before, object? after)
        => _db.AccountingConfigAudit.Add(new AccountingConfigAuditEntry
        {
            Entity = entity,
            EntityId = entityId,
            Action = action,
            ChangedBy = string.IsNullOrWhiteSpace(user) ? "unknown" : user,
            ChangedAt = DateTimeOffset.UtcNow,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before, Json),
            AfterJson = after is null ? null : JsonSerializer.Serialize(after, Json)
        });

    private static object Snapshot(StockAccountProfile p) => new
    {
        p.Id, p.Name, p.StockAccount, p.AgnumSndId, p.PriceDifferenceAccount, p.WriteOffAccount, p.MarkdownAccount, p.IsDefault
    };

    private static object Snapshot(AgnumPostingRule r) => new
    {
        r.Id, r.StockAccountProfileId, VatTreatment = r.VatTreatment.ToString(), r.SchemeId, r.VatCode
    };

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
