using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Configuration;
using LKvitai.MES.Modules.Warehouse.Projections;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Pricing;
using Marten;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public partial class ReceivingWorkflowIntegrationTests
{
    private async Task<ServiceProvider> ApplicationValuationProviderAsync()
    {
        _store!.Dispose();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        { ["ConnectionStrings:WarehouseDb"] = _postgres!.GetConnectionString() }).Build();
        var services = new ServiceCollection().AddLogging();
        services.AddMartenEventStore(config, options => options.RegisterProjections());
        services.AddMassTransitConfiguration(config);
        services.AddScoped(_ => CreateDbContext());
        services.AddSingleton<ICurrentUserService>(new StaticCurrentUserService("QA-C8"));
        services.AddScoped<IAvailableStockQuantityResolver, MartenAvailableStockQuantityResolver>();
        services.AddSingleton<IHttpContextAccessor>(new HttpContextAccessor { HttpContext = new DefaultHttpContext() });
        services.AddMediatR(x => x.RegisterServicesFromAssemblyContaining<RevaluationCommandHandler>());
        var provider = services.BuildServiceProvider();
        _store = provider.GetRequiredService<IDocumentStore>();
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        return provider;
    }

    [SkippableFact]
    public async Task ApplicationMarten_TwoPricedReceiptsBothSucceedAndUpdateWac()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync(false, false);
        await using var provider = await ApplicationValuationProviderAsync();
        foreach (var (qty, price) in new[] { (2m, 10m), (3m, 20m) })
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WarehouseDbContext>();
            var shipment = await CreateShipmentAsync(db, $"QA-C8-priced-{price}", 1, 1, qty);
            shipment.Lines.Single().UnitPrice = price;
            await db.SaveChangesAsync();
            var controller = new ReceivingController(WarehouseCodeProvider.For("WH1"), db, _store!, new StaticCurrentUserService("QA-C8"),
                scope.ServiceProvider.GetRequiredService<IMediator>(), NullLogger<ReceivingController>.Instance,
                new ItemPriceSnapshotService(db), BalanceLocks(), PurchaseService(db))
            { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
            (await controller.ReceiveGoodsAsync(shipment.Id,
                new(shipment.Lines.Single().Id, qty, null, null, null, "QA-C8-priced", null, null)))
                .Should().BeOfType<OkObjectResult>();
        }
        await using var query = _store!.QuerySession();
        var aggregate = await query.Events.AggregateStreamAsync<ItemValuation>(ItemValuation.StreamIdFor(1));
        aggregate!.ValuedQty.Should().Be(5m);
        aggregate.ValueEur.Should().Be(80m);
        aggregate.UnitCost.Should().Be(16m);
        using var revalueScope = provider.CreateScope();
        (await revalueScope.ServiceProvider.GetRequiredService<IMediator>().Send(
            new RevaluationCommand { ItemId = 1, NewUnitPrice = 12m, Reason = "QA-C8-valuation" })).IsSuccess.Should().BeTrue();
        await using var after = _store!.QuerySession();
        (await after.Events.AggregateStreamAsync<ItemValuation>(ItemValuation.StreamIdFor(1)))!.ValueEur.Should().Be(60m);
    }
}
