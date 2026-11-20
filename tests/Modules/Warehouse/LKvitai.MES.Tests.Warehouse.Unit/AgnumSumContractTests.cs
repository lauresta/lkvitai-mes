using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Fx;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Unit;

/// <summary>Wire-format and DI-activation guards found by the first browser test of the Agnum sum features.</summary>
public sealed class AgnumSumContractTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("Domestic", VatTreatment.Domestic)]
    [InlineData("EuAcquisition", VatTreatment.EuAcquisition)]
    [InlineData("Import", VatTreatment.Import)]
    public void RuleRequest_AcceptsEnumAsString(string wire, VatTreatment expected)
    {
        var json = $$"""{"id":0,"stockAccountProfileId":3,"vatTreatment":"{{wire}}","schemeId":7,"vatCode":"PVM1"}""";
        var request = JsonSerializer.Deserialize<AgnumAccountingController.RuleRequest>(json, Web)!;
        request.VatTreatment.Should().Be(expected);
    }

    [Fact]
    public void RuleRequest_StillAcceptsNumericEnum()
    {
        var request = JsonSerializer.Deserialize<AgnumAccountingController.RuleRequest>(
            """{"stockAccountProfileId":1,"vatTreatment":1,"schemeId":2}""", Web)!;
        request.VatTreatment.Should().Be(VatTreatment.EuAcquisition);
    }

    [Fact]
    public void PostingRule_IsWrittenWithEnumAsString()
    {
        var json = JsonSerializer.Serialize(new AgnumPostingRule { Id = 1, VatTreatment = VatTreatment.Import }, Web);
        json.Should().Contain("\"vatTreatment\":\"Import\"");
    }

    [Fact]
    public void ControllersAndNewServices_HaveOneActivatableConstructor()
    {
        // Typed HttpClients and controllers are built with ActivatorUtilities, which throws on ambiguous constructors.
        var assemblies = new[] { typeof(AgnumAccountingController).Assembly, typeof(LbFxRateProvider).Assembly };
        var offenders = assemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.Namespace is not null
                && (typeof(Microsoft.AspNetCore.Mvc.ControllerBase).IsAssignableFrom(t)
                    || t.Namespace.StartsWith("LKvitai.MES.Modules.Warehouse.Infrastructure.Agnum.Accounting")
                    || t.Namespace.StartsWith("LKvitai.MES.Modules.Warehouse.Infrastructure.Fx")
                    || t.Namespace.StartsWith("LKvitai.MES.Modules.Warehouse.Infrastructure.MonthEnd")
                    || t.Namespace.StartsWith("LKvitai.MES.Modules.Warehouse.Infrastructure.Purchasing")))
            .Where(t => !typeof(Exception).IsAssignableFrom(t))
            .Where(t =>
            {
                var ctors = t.GetConstructors(BindingFlags.Public | BindingFlags.Instance);
                return ctors.Length > 1 && ctors.Count(c => c.IsDefined(typeof(ActivatorUtilitiesConstructorAttribute), false)) != 1;
            })
            .Select(t => t.FullName).ToList();
        offenders.Should().BeEmpty();
    }

    [Fact]
    public void TypedHttpClients_CanBeResolved()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<WarehouseDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddHttpClient<IFxRateProvider, LbFxRateProvider>(c => c.BaseAddress = new Uri("http://fx.invalid/"));
        services.AddHttpClient<IAgnumReferenceClient, AgnumReferenceClient>(c => c.BaseAddress = new Uri("http://agnum.invalid/"));
        services.AddHttpClient<IAgnumPostingClient, AgnumPostingClient>(c => c.BaseAddress = new Uri("http://agnum.invalid/"));
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<AgnumOutboxService>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IFxRateProvider>().Should().BeOfType<LbFxRateProvider>();
        scope.ServiceProvider.GetRequiredService<IAgnumReferenceClient>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<IAgnumPostingClient>().Should().NotBeNull();
        scope.ServiceProvider.GetRequiredService<AgnumOutboxService>().Should().NotBeNull();
    }
}
