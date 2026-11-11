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
        services.AddMediatR(x => x.RegisterServicesFromAssemblyContaining<InitializeValuationCommandHandler>());
        var provider = services.BuildServiceProvider();
        _store = provider.GetRequiredService<IDocumentStore>();
        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        return provider;
    }

    [SkippableFact]
    public async Task ApplicationMarten_ValuationHandlersPersistSecondAndLaterCosts()
    {
        DockerRequirement.EnsureEnabled();
        await SeedBaseDataAsync(false, false);
        await using var provider = await ApplicationValuationProviderAsync();
        await using (var seed = _store!.LightweightSession())
        {
            seed.Events.Append("QA-C8-stock", new LKvitai.MES.Contracts.Events.GoodsReceivedEvent
            { SKU = "RM-0001", WarehouseId = "WH1", DestinationLocation = "BIN", ReceivedQty = 2 });
            await seed.SaveChangesAsync();
        }
        await using (var db = CreateDbContext())
        {
            db.Locations.Add(new() { Id = 4, Code = "BIN", Barcode = "BIN", Type = "Bin", IsVirtual = false, Status = "Active" });
            await db.SaveChangesAsync();
        }
        using var scope = provider.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        (await mediator.Send(new InitializeValuationCommand { ItemId = 1, InitialCost = 10, Reason = "QA-C8-valuation" })).IsSuccess.Should().BeTrue();
        var second = await mediator.Send(new AdjustValuationCostCommand { ItemId = 1, NewCost = 11, Reason = "QA-C8-valuation", ApprovedBy = "QA-C8" });
        second.IsSuccess.Should().BeTrue(second.Error);
        (await mediator.Send(new AdjustValuationCostCommand { ItemId = 1, NewCost = 12, Reason = "QA-C8-valuation", ApprovedBy = "QA-C8" })).IsSuccess.Should().BeTrue();
        // Each request gets its own EF context as it does in the API.
        using var landedScope = provider.CreateScope();
        (await landedScope.ServiceProvider.GetRequiredService<IMediator>().Send(new ApplyLandedCostCommand { ShipmentId = Guid.NewGuid(), FreightCost = 2 })).IsSuccess.Should().BeTrue();
        using var writeScope = provider.CreateScope();
        (await writeScope.ServiceProvider.GetRequiredService<IMediator>().Send(new WriteDownCommand { ItemId = 1, NewValue = 12, Reason = "QA-C8-valuation", ApprovedBy = "QA-C8" })).IsSuccess.Should().BeTrue();
        await using var query = _store.QuerySession();
        var aggregate = await query.Events.AggregateStreamAsync<ItemValuation>(ItemValuation.StreamIdFor(1));
        aggregate!.ItemId.Should().Be(1);
        aggregate.CurrentCost.Should().Be(12);
        (await query.Events.FetchStreamStateAsync(ItemValuation.StreamIdFor(1)))!.Version.Should().Be(5);
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
                new ItemPriceSnapshotService(db), BalanceLocks())
            { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
            (await controller.ReceiveGoodsAsync(shipment.Id,
                new(shipment.Lines.Single().Id, qty, null, null, null, "QA-C8-priced", null, null)))
                .Should().BeOfType<OkObjectResult>();
        }
        await using var query = _store!.QuerySession();
        var aggregate = await query.Events.AggregateStreamAsync<ItemValuation>(ItemValuation.StreamIdFor(1));
        aggregate!.CurrentCost.Should().Be(16m);
    }
}
