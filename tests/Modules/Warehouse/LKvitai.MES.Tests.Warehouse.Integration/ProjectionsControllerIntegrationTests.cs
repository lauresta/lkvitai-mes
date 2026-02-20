using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Application.Commands;
using LKvitai.MES.Modules.Warehouse.Application.Projections;
using LKvitai.MES.Modules.Warehouse.Application.Queries;
using LKvitai.MES.Contracts.Events;
using LKvitai.MES.Modules.Warehouse.Domain;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Projections;
using LKvitai.MES.Modules.Warehouse.Projections;
using LKvitai.MES.SharedKernel;
using Marten;
using Marten.Events.Projections;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class ProjectionsControllerIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private IDocumentStore? _store;
    private ServiceProvider? _provider;

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled)
        {
            return;
        }

        _postgres = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .Build();

        await _postgres.StartAsync();

        _store = DocumentStore.For(opts =>
        {
            opts.Connection(_postgres.GetConnectionString());
            opts.Events.StreamIdentity = Marten.Events.StreamIdentity.AsString;
            opts.Events.DatabaseSchemaName = "warehouse_events";

            opts.Projections.Add<LocationBalanceProjection>(ProjectionLifecycle.Async);
            opts.Projections.Add<AvailableStockProjection>(ProjectionLifecycle.Async);
        });

        await _store.Storage.ApplyAllConfiguredChangesToDatabaseAsync();

        var services = new ServiceCollection();
        services.AddSingleton(_store);
        services.AddScoped<IProjectionRebuildService, ProjectionRebuildService>();
        services.AddLogging();
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssemblyContaining<RebuildProjectionCommandHandler>();
            cfg.RegisterServicesFromAssemblyContaining<VerifyProjectionQueryHandler>();
        });

        _provider = services.BuildServiceProvider();
    }

    public async Task DisposeAsync()
    {
        _provider?.Dispose();
        _store?.Dispose();
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Rebuild_InvalidName_ReturnsProblemDetails400()
    {
        DockerRequirement.EnsureEnabled();

        using var scope = _provider!.CreateScope();
        var controller = CreateController(scope.ServiceProvider);

        var result = await controller.RebuildAsync(
            new ProjectionsController.RebuildProjectionRequestDto("InvalidProjection"),
            CancellationToken.None);

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        GetErrorCode(objectResult.Value).Should().Be(DomainErrorCodes.InvalidProjectionName);
    }

    [SkippableFact]
    public async Task Rebuild_HappyPath_ReturnsSynchronousReport()
    {
        DockerRequirement.EnsureEnabled();

        await SeedLocationBalanceEventAsync();
        await RunDaemonAsync();

        using var scope = _provider!.CreateScope();
        var controller = CreateController(scope.ServiceProvider);

        var result = await controller.RebuildAsync(
            new ProjectionsController.RebuildProjectionRequestDto("LocationBalance"),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ProjectionRebuildReport>().Subject;
        payload.ProjectionName.Should().Be("LocationBalance");
        payload.ChecksumMatch.Should().BeTrue();
    }

    [SkippableFact]
    public async Task Rebuild_AvailableStock_NoEvents_ReturnsSynchronousReport()
    {
        DockerRequirement.EnsureEnabled();

        using var scope = _provider!.CreateScope();
        var controller = CreateController(scope.ServiceProvider);

        var result = await controller.RebuildAsync(
            new ProjectionsController.RebuildProjectionRequestDto("AvailableStock"),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<ProjectionRebuildReport>().Subject;
        payload.ProjectionName.Should().Be("AvailableStock");
        payload.ChecksumMatch.Should().BeTrue();
    }

    [SkippableFact]
    public async Task Verify_HappyPath_ReturnsChecksumsAndRowCounts()
    {
        DockerRequirement.EnsureEnabled();

        await SeedLocationBalanceEventAsync();
        await RunDaemonAsync();

        // Build shadow table without swap so /verify can compare production vs shadow.
        var rebuildService = new ProjectionRebuildService(
            _store!,
            NullLogger<ProjectionRebuildService>.Instance);
        var warmup = await rebuildService.RebuildProjectionAsync("LocationBalance", verify: false);
        warmup.IsSuccess.Should().BeTrue();

        using var scope = _provider!.CreateScope();
        var controller = CreateController(scope.ServiceProvider);

        var result = await controller.VerifyAsync(
            new ProjectionsController.VerifyProjectionRequestDto("LocationBalance"),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<VerifyProjectionResultDto>().Subject;
        payload.ChecksumMatch.Should().BeTrue();
        payload.ProductionRowCount.Should().BeGreaterThan(0);
        payload.ShadowRowCount.Should().BeGreaterThan(0);
    }

    private ProjectionsController CreateController(IServiceProvider services)
    {
        var mediator = services.GetRequiredService<IMediator>();
        return new ProjectionsController(mediator)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };
    }

    private async Task RunDaemonAsync()
    {
        using var daemon = await _store!.BuildProjectionDaemonAsync();
        await daemon.StartAllAsync();
        await daemon.WaitForNonStaleData(TimeSpan.FromSeconds(15));
        await daemon.StopAllAsync();
    }

    private async Task SeedLocationBalanceEventAsync()
    {
        await using var session = _store!.LightweightSession();
        session.Events.Append(StockLedgerStreamId.For("WH1", "LOC-PROJ", "SKU-PROJ"), new StockMovedEvent
        {
            MovementId = Guid.NewGuid(),
            SKU = "SKU-PROJ",
            Quantity = 20m,
            FromLocation = "SUPPLIER",
            ToLocation = "LOC-PROJ",
            MovementType = "RECEIPT",
            OperatorId = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow
        });
        await session.SaveChangesAsync();
    }

    private static string? GetErrorCode(object? resultValue)
    {
        var problem = resultValue as ProblemDetails;
        if (problem?.Extensions is null || !problem.Extensions.TryGetValue("errorCode", out var codeValue))
        {
            return null;
        }

        return codeValue?.ToString();
    }
}
