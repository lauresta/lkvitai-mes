using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Application.Projections;
using LKvitai.MES.Modules.Warehouse.Domain.Aggregates;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Projections;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public partial class ReceivingWorkflowIntegrationTests
{
    private async Task<ProjectionRebuildService> SeedOnHandRebuildAsync()
    {
        await SeedBaseDataAsync(false, false);
        await using (var db = CreateDbContext())
        {
            db.Locations.Add(new() { Id = 4, Code = "BIN", Barcode = "BIN", Type = "Bin", IsVirtual = false, Status = "Active" });
            db.Items.Add(new() { Id = 2, InternalSKU = "RM-0002", Name = "Second", CategoryId = 1, BaseUoM = "PCS", Status = "Active" });
            db.Items.Add(new() { Id = 99, InternalSKU = "ORPHAN", Name = "No valuation events", CategoryId = 1, BaseUoM = "PCS", Status = "Active" });
            db.OnHandValues.AddRange(
                new OnHandValue { Id = ValuationItemId.ToValuationItemId(1), ItemId = 1, ItemSku = "RM-0001", ItemName = "First", Qty = 1, UnitCost = 10, TotalValue = 10, LastUpdated = DateTimeOffset.UtcNow },
                new OnHandValue { Id = Guid.NewGuid(), ItemId = 99, ItemSku = "ORPHAN", ItemName = "Orphan", Qty = 2, UnitCost = 5, TotalValue = 10, LastUpdated = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        await using (var session = _store!.LightweightSession())
        {
            session.Events.Append("QA-C8-b-stock", new GoodsReceivedEvent
                { SKU = "RM-0001", WarehouseId = "WH1", DestinationLocation = "BIN", ReceivedQty = 3 });
            session.Events.Append(ItemValuation.StreamIdFor(1), new ValuationInitialized
                { ItemId = ValuationItemId.ToValuationItemId(1), InitialUnitCost = 10, Reason = "QA-C8-b" });
            session.Events.Append(ItemValuation.StreamIdFor(2), new ValuationInitialized
                { ItemId = ValuationItemId.ToValuationItemId(2), InitialUnitCost = 4, Reason = "QA-C8-b" });
            await session.SaveChangesAsync();
        }
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            { ["ConnectionStrings:WarehouseDb"] = _postgres!.GetConnectionString() }).Build();
        return new ProjectionRebuildService(_store!, NullLogger<ProjectionRebuildService>.Instance,
            new LKvitai.MES.Modules.Warehouse.Infrastructure.Locking.PostgresDistributedLock(config), config);
    }

    private static readonly ProjectionAuditActor RebuildActor = new("QA-C8-admin", "127.0.0.1", "QA-C8-test");
    private static OnHandValueApply ApplyRequest(OnHandValuePreview preview) => new(preview.ProductionChecksum, preview.ShadowChecksum, "QA-C8-correct register");

    [SkippableFact]
    public async Task OnHandRebuild_PreviewAndApplyPersistAllChangesAndAudit()
    {
        DockerRequirement.EnsureEnabled();
        var service = await SeedOnHandRebuildAsync();
        var preview = await service.PreviewAsync();
        var changed = preview.Rows.Single(x => x.Sku == "RM-0001");
        changed.Change.Should().Be("Changed");
        changed.Before.Should().Be(new OnHandValueAmounts(1, 10, 10));
        changed.After.Should().Be(new OnHandValueAmounts(3, 10, 30));
        preview.Rows.Single(x => x.Sku == "ORPHAN").Change.Should().Be("Removed");
        preview.Rows.Single(x => x.Sku == "RM-0002").Change.Should().Be("Added");
        (await service.ApplyAsync(ApplyRequest(preview), RebuildActor)).Should().BeTrue();
        await using var db = CreateDbContext();
        var rows = await db.OnHandValues.OrderBy(x => x.ItemId).ToListAsync();
        rows.Should().HaveCount(2);
        rows[0].Qty.Should().Be(3);
        rows[0].TotalValue.Should().Be(30);
        var audit = await db.SecurityAuditLogs.SingleAsync();
        audit.UserId.Should().Be(RebuildActor.UserId);
        audit.Details.Should().Contain("QA-C8-correct register").And.Contain(preview.ProductionChecksum).And.Contain(preview.ShadowChecksum).And.Contain("\"rowCount\":2");
        (await service.ApplyAsync(ApplyRequest(preview), RebuildActor)).Should().BeFalse();
        // LIKE INCLUDING ALL does not copy foreign keys. The applied register must retain them.
        await db.Items.Where(x => x.Id == 2).ExecuteDeleteAsync();
        (await db.OnHandValues.AnyAsync(x => x.ItemId == 2)).Should().BeFalse();
    }

    [SkippableTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OnHandRebuild_ChangedEitherChecksumReturns409WithoutSwap(bool changeShadow)
    {
        DockerRequirement.EnsureEnabled();
        var service = await SeedOnHandRebuildAsync();
        var preview = await service.PreviewAsync();
        await using (var connection = new NpgsqlConnection(_postgres!.GetConnectionString()))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = changeShadow
                ? "UPDATE public.on_hand_value_shadow SET \"Qty\"=4 WHERE \"ItemId\"=1"
                : "UPDATE public.on_hand_value SET \"Qty\"=4 WHERE \"ItemId\"=1";
            await command.ExecuteNonQueryAsync();
        }
        using var server = RebuildServer(service);
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "QA-C8-admin");
        client.DefaultRequestHeaders.Add("X-User-Roles", WarehouseRoles.WarehouseAdmin);
        var response = await client.PostAsJsonAsync("/api/warehouse/v1/admin/projections/on-hand-value/apply", ApplyRequest(preview));
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await using var db = CreateDbContext();
        (await db.OnHandValues.SingleAsync(x => x.ItemId == 1)).Qty.Should().Be(changeShadow ? 1 : 4);
        (await db.SecurityAuditLogs.CountAsync()).Should().Be(0);
    }

    [SkippableFact]
    public async Task OnHandRebuild_OperatorForbiddenAndBlankReasonRejected()
    {
        DockerRequirement.EnsureEnabled();
        var service = await SeedOnHandRebuildAsync();
        var preview = await service.PreviewAsync();
        using var server = RebuildServer(service);
        using var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "QA-C8-operator");
        client.DefaultRequestHeaders.Add("X-User-Roles", WarehouseRoles.Operator);
        foreach (var action in new[] { "preview", "apply" })
            (await client.PostAsJsonAsync($"/api/warehouse/v1/admin/projections/on-hand-value/{action}", ApplyRequest(preview)))
                .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        client.DefaultRequestHeaders.Remove("X-User-Roles");
        client.DefaultRequestHeaders.Add("X-User-Roles", WarehouseRoles.WarehouseAdmin);
        (await client.PostAsJsonAsync("/api/warehouse/v1/admin/projections/on-hand-value/apply", ApplyRequest(preview) with { Reason = " " }))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await using var db = CreateDbContext();
        (await db.OnHandValues.SingleAsync(x => x.ItemId == 1)).TotalValue.Should().Be(10);
    }

    [SkippableFact]
    public async Task OnHandRebuild_ConcurrentApplyCommitsOnlyOnce()
    {
        DockerRequirement.EnsureEnabled();
        var service = await SeedOnHandRebuildAsync();
        var preview = await service.PreviewAsync();
        var results = await Task.WhenAll(service.ApplyAsync(ApplyRequest(preview), RebuildActor),
            service.ApplyAsync(ApplyRequest(preview), RebuildActor));
        results.Count(x => x).Should().Be(1);
        await using var db = CreateDbContext();
        (await db.SecurityAuditLogs.CountAsync()).Should().Be(1);
        (await db.OnHandValues.SingleAsync(x => x.ItemId == 1)).TotalValue.Should().Be(30);
    }

    [SkippableFact]
    public async Task OnHandRebuild_AuditFailureRollsBackSwap()
    {
        DockerRequirement.EnsureEnabled();
        var service = await SeedOnHandRebuildAsync();
        var preview = await service.PreviewAsync();
        var act = () => service.ApplyAsync(ApplyRequest(preview), RebuildActor with { UserId = new string('x', 201) });
        await act.Should().ThrowAsync<PostgresException>();
        await using var db = CreateDbContext();
        (await db.OnHandValues.SingleAsync(x => x.ItemId == 1)).TotalValue.Should().Be(10);
        (await db.SecurityAuditLogs.CountAsync()).Should().Be(0);
        (await service.ApplyAsync(ApplyRequest(preview), RebuildActor)).Should().BeTrue();
    }

    private static TestServer RebuildServer(IOnHandValueRebuildService service)
    {
        return new TestServer(new WebHostBuilder().UseEnvironment("Development").ConfigureServices(services =>
        {
            services.AddLogging();
            services.AddSingleton(service);
            services.AddMediatR(x => x.RegisterServicesFromAssemblyContaining<ProjectionsController>());
            services.AddControllers().AddApplicationPart(typeof(ProjectionsController).Assembly);
            services.AddAuthentication(WarehouseAuthenticationDefaults.Scheme)
                .AddScheme<AuthenticationSchemeOptions, WarehouseAuthenticationHandler>(WarehouseAuthenticationDefaults.Scheme, _ => { });
            services.AddAuthorization(options => options.AddPolicy(WarehousePolicies.AdminOnly,
                policy => policy.RequireRole(WarehouseRoles.WarehouseAdmin)));
        }).Configure(app =>
        {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));
    }
}
