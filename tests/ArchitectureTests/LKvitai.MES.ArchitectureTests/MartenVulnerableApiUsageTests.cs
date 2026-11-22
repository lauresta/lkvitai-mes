using System.Text.RegularExpressions;
using Xunit;

namespace LKvitai.MES.ArchitectureTests;

/// <summary>
/// Guards the documented exception for Marten 7.0.0 (see docs/security/dependency-exceptions.md).
/// Marten is pinned at 7.x because the fixed releases (8.37+) are a major upgrade. The advisories
/// below are only exploitable through specific Marten APIs; these tests fail if production code
/// starts using them, so the exception cannot silently become a live vulnerability.
///
///   GHSA-vmw2-qwm8-x84c : full-text search (<c>regConfig</c>) SQL injection
///   GHSA-rfx3-98h7-v3xp : LINQ dictionary-key / tenant-id SQL string interpolation
/// </summary>
public class MartenVulnerableApiUsageTests
{
    // Marten full-text search APIs (GHSA-vmw2-qwm8-x84c).
    private static readonly Regex FullTextSearchApi = new(
        @"\b(PlainTextSearch|PlainTextSearchAsync|PhraseSearch|PhraseSearchAsync|WebStyleSearch|WebStyleSearchAsync|" +
        @"PrefixSearch|PrefixSearchAsync|NgramSearch|SearchAsync|FullTextIndex|FullTextSearch|regConfig)\b|" +
        @"\.Search\s*\(",
        RegexOptions.Compiled);

    // Marten multi-tenancy / projection-teardown APIs that take a tenant id (GHSA-rfx3-98h7-v3xp).
    private static readonly Regex TenantApi = new(
        @"\b(DeleteProjectionProgressAsync|MultiTenanted|AllDocumentsAreMultiTenanted|ForTenant|ForTenants|" +
        @"WithTenantId|UseTenant|TenantIds|DeleteAllForTenant)\b",
        RegexOptions.Compiled);

    [Fact]
    public void Production_Code_Does_Not_Use_Marten_FullText_Search()
    {
        var offenders = FindMartenFilesMatching(FullTextSearchApi);

        Assert.True(
            offenders.Count == 0,
            "Marten full-text search is vulnerable in 7.x (GHSA-vmw2-qwm8-x84c). Upgrade Marten to >= 8.37 " +
            "(see docs/security/dependency-exceptions.md) before using it. Offending usage:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Production_Code_Does_Not_Use_Marten_Tenancy_Apis()
    {
        var offenders = FindMartenFilesMatching(TenantApi);

        Assert.True(
            offenders.Count == 0,
            "Marten tenant-id handling is vulnerable in 7.x (GHSA-rfx3-98h7-v3xp). Upgrade Marten to >= 8.37.4 " +
            "(see docs/security/dependency-exceptions.md) before using it. Offending usage:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    private static List<string> FindMartenFilesMatching(Regex pattern)
    {
        var repoRoot = ArchitectureProjectRules.ResolveRepoRoot();
        var srcRoot = Path.Combine(repoRoot, "src");
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(srcRoot, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(repoRoot, file);
            var segments = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Contains("bin") || segments.Contains("obj"))
            {
                continue;
            }

            var text = File.ReadAllText(file);
            if (!text.Contains("Marten", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    offenders.Add($"{relative}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        return offenders;
    }
}