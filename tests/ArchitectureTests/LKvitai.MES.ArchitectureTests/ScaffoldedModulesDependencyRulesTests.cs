using Xunit;

namespace LKvitai.MES.ArchitectureTests;

/// <summary>
/// Enforces the scaffolding-era dependency rules for the three new modules
/// (Sales, Frontline, Scanning):
///
///   Modules.X.WebUI         -> Modules.X.Contracts (+ BuildingBlocks)
///   Modules.X.Api           -> Modules.X.Application + Modules.X.Contracts (+ BuildingBlocks)
///   Modules.X.Application   -> Modules.X.Contracts (+ BuildingBlocks)
///   Modules.X.Infrastructure-> Modules.X.Application + Modules.X.Contracts (+ BuildingBlocks)
///   Modules.X.Contracts     -> BuildingBlocks only (no module project refs)
///
/// None of the projects above may reference projects in a *different* module.
///
/// Warehouse is intentionally excluded from these tests — it predates the
/// five-layer scaffold rules and has its own structure (Domain, Sagas,
/// Projections, Integration) validated by the older arch tests in this
/// project.
/// </summary>
public class ScaffoldedModulesDependencyRulesTests
{
    public static IEnumerable<object[]> ScaffoldedModules() =>
        new[] { new object[] { "Sales" }, new object[] { "Frontline" }, new object[] { "Scanning" } };

    [Theory]
    [MemberData(nameof(ScaffoldedModules))]
    public void Contracts_Only_References_BuildingBlocks(string module)
    {
        var refs = LoadProjectRefs(module, "Contracts");

        Assert.DoesNotContain(refs, r => IsModuleProject(r));
    }

    [Theory]
    [MemberData(nameof(ScaffoldedModules))]
    public void Application_Only_References_Contracts_Of_Same_Module_And_BuildingBlocks(string module)
    {
        var refs = LoadProjectRefs(module, "Application");

        // May reference BuildingBlocks and this module's Contracts only.
        foreach (var r in refs)
        {
            if (!IsModuleProject(r)) continue;

            Assert.True(
                IsSameModuleContracts(r, module),
                $"Application in {module} has disallowed module reference: {r}");
        }

        Assert.DoesNotContain(refs, r => IsModuleInfrastructure(r));
        Assert.DoesNotContain(refs, r => IsModuleApi(r));
        Assert.DoesNotContain(refs, r => IsModuleWebUI(r));
    }

    [Theory]
    [MemberData(nameof(ScaffoldedModules))]
    public void Infrastructure_References_Stay_Within_Same_Module_And_BuildingBlocks(string module)
    {
        var refs = LoadProjectRefs(module, "Infrastructure");

        foreach (var r in refs)
        {
            if (!IsModuleProject(r)) continue;

            Assert.True(
                IsSameModuleApplication(r, module) || IsSameModuleContracts(r, module),
                $"Infrastructure in {module} has disallowed module reference: {r}");
        }

        Assert.DoesNotContain(refs, r => IsModuleApi(r));
        Assert.DoesNotContain(refs, r => IsModuleWebUI(r));
    }

    [Theory]
    [MemberData(nameof(ScaffoldedModules))]
    public void Api_References_Stay_Within_Same_Module_And_BuildingBlocks(string module)
    {
        var refs = LoadProjectRefs(module, "Api");

        foreach (var r in refs)
        {
            if (!IsModuleProject(r)) continue;

            Assert.True(
                IsSameModuleApplication(r, module) || IsSameModuleContracts(r, module),
                $"Api in {module} has disallowed module reference: {r}");
        }

        Assert.DoesNotContain(refs, r => IsModuleInfrastructure(r));
        Assert.DoesNotContain(refs, r => IsModuleWebUI(r));
    }

    [Theory]
    [MemberData(nameof(ScaffoldedModules))]
    public void WebUI_Only_References_Contracts_Of_Same_Module_And_BuildingBlocks(string module)
    {
        var refs = LoadProjectRefs(module, "WebUI");

        foreach (var r in refs)
        {
            if (!IsModuleProject(r)) continue;

            Assert.True(
                IsSameModuleContracts(r, module),
                $"WebUI in {module} has disallowed module reference: {r}");
        }

        Assert.DoesNotContain(refs, r => IsModuleApplication(r));
        Assert.DoesNotContain(refs, r => IsModuleInfrastructure(r));
        Assert.DoesNotContain(refs, r => IsModuleApi(r));
    }

    [Theory]
    [MemberData(nameof(ScaffoldedModules))]
    public void No_Cross_Module_References(string module)
    {
        foreach (var layer in new[] { "Contracts", "Application", "Infrastructure", "Api", "WebUI" })
        {
            var refs = LoadProjectRefs(module, layer);

            foreach (var r in refs)
            {
                if (!IsModuleProject(r)) continue;

                Assert.True(
                    r.Contains($"Modules.{module}.", StringComparison.OrdinalIgnoreCase),
                    $"{module}.{layer} has cross-module reference to: {r}");
            }
        }
    }

    private static IReadOnlyList<string> LoadProjectRefs(string module, string layer)
    {
        var repoRoot = ArchitectureProjectRules.ResolveRepoRoot();
        var project = ArchitectureProjectRules.LoadProject(
            repoRoot,
            Path.Combine(
                "src", "Modules", module,
                $"LKvitai.MES.Modules.{module}.{layer}",
                $"LKvitai.MES.Modules.{module}.{layer}.csproj"));

        return ArchitectureProjectRules.GetProjectReferences(project);
    }

    private static bool IsModuleProject(string reference) =>
        reference.Contains("LKvitai.MES.Modules.", StringComparison.OrdinalIgnoreCase);

    private static bool IsSameModuleContracts(string reference, string module) =>
        reference.EndsWith($"LKvitai.MES.Modules.{module}.Contracts.csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsSameModuleApplication(string reference, string module) =>
        reference.EndsWith($"LKvitai.MES.Modules.{module}.Application.csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsModuleApplication(string reference) =>
        reference.EndsWith(".Application.csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsModuleInfrastructure(string reference) =>
        reference.EndsWith(".Infrastructure.csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsModuleApi(string reference) =>
        reference.EndsWith(".Api.csproj", StringComparison.OrdinalIgnoreCase);

    private static bool IsModuleWebUI(string reference) =>
        reference.EndsWith(".WebUI.csproj", StringComparison.OrdinalIgnoreCase);
}
