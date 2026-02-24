using System.Net;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Api.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class AdminSchemaSmokeIntegrationTests
{
    [Theory]
    [InlineData("/api/warehouse/v1/admin/backups")]
    [InlineData("/api/warehouse/v1/admin/retention-policies")]
    [InlineData("/api/warehouse/v1/admin/gdpr/erasure-requests")]
    [InlineData("/api/warehouse/v1/admin/dr/drills")]
    public async Task AdminEndpoints_WhenSchemaAvailable_Returns200(string endpoint)
    {
        using var server = BuildServer();
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add("X-User-Id", "admin-1");
        client.DefaultRequestHeaders.Add("X-User-Roles", WarehouseRoles.WarehouseAdmin);

        var response = await client.GetAsync(endpoint);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static TestServer BuildServer()
    {
        var webHostBuilder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddControllers()
                    .AddApplicationPart(typeof(AdminBackupsController).Assembly);

                services
                    .AddAuthentication(WarehouseAuthenticationDefaults.Scheme)
                    .AddScheme<AuthenticationSchemeOptions, WarehouseAuthenticationHandler>(
                        WarehouseAuthenticationDefaults.Scheme,
                        _ => { });

                services.AddAuthorization(options =>
                {
                    options.DefaultPolicy = new AuthorizationPolicyBuilder(WarehouseAuthenticationDefaults.Scheme)
                        .RequireAuthenticatedUser()
                        .Build();

                    options.AddPolicy(WarehousePolicies.AdminOnly, policy =>
                        policy.RequireRole(WarehouseRoles.WarehouseAdmin));
                });

                services.AddSingleton<IBackupService>(new StubBackupService());
                services.AddSingleton<IRetentionPolicyService>(new StubRetentionPolicyService());
                services.AddSingleton<IGdprErasureService>(new StubGdprErasureService());
                services.AddSingleton<IDisasterRecoveryService>(new StubDisasterRecoveryService());
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(endpoints => endpoints.MapControllers());
            });

        return new TestServer(webHostBuilder);
    }

    private sealed class StubBackupService : IBackupService
    {
        public Task<BackupExecutionDto> TriggerBackupAsync(string trigger, CancellationToken cancellationToken = default)
            => Task.FromResult(new BackupExecutionDto(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                "FULL",
                0,
                string.Empty,
                "COMPLETED",
                null,
                trigger));

        public Task<IReadOnlyList<BackupExecutionDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<BackupExecutionDto>>(Array.Empty<BackupExecutionDto>());

        public Task<BackupRestoreResultDto> RestoreAsync(Guid backupId, string targetEnvironment, CancellationToken cancellationToken = default)
            => Task.FromResult(new BackupRestoreResultDto(true, "ok"));

        public Task<BackupRestoreResultDto> RunMonthlyRestoreTestAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new BackupRestoreResultDto(true, "ok"));
    }

    private sealed class StubRetentionPolicyService : IRetentionPolicyService
    {
        public Task<IReadOnlyList<RetentionPolicyDto>> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RetentionPolicyDto>>(Array.Empty<RetentionPolicyDto>());

        public Task<RetentionPolicyDto> CreateAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new RetentionPolicyDto(
                1,
                request.DataType.ToString().ToUpperInvariant(),
                request.RetentionPeriodDays,
                request.ArchiveAfterDays,
                request.DeleteAfterDays,
                request.Active,
                "admin-1",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));

        public Task<RetentionPolicyDto?> UpdateAsync(int id, UpdateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult<RetentionPolicyDto?>(new RetentionPolicyDto(
                id,
                request.DataType.ToString().ToUpperInvariant(),
                request.RetentionPeriodDays,
                request.ArchiveAfterDays,
                request.DeleteAfterDays,
                request.Active,
                "admin-1",
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow));

        public Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task<RetentionExecutionDto> ExecuteAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new RetentionExecutionDto(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                0,
                0,
                "COMPLETED",
                null));

        public Task<bool> SetAuditLogLegalHoldAsync(long id, bool legalHold, CancellationToken cancellationToken = default)
            => Task.FromResult(true);
    }

    private sealed class StubGdprErasureService : IGdprErasureService
    {
        public Task<ErasureRequestDto> RequestAsync(CreateErasureRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(new ErasureRequestDto(
                Guid.NewGuid(),
                request.CustomerId,
                request.Reason,
                "PENDING",
                DateTimeOffset.UtcNow,
                "admin-1",
                null,
                null,
                null,
                null));

        public Task<IReadOnlyList<ErasureRequestDto>> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ErasureRequestDto>>(Array.Empty<ErasureRequestDto>());

        public Task<ErasureRequestDto?> ApproveAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult<ErasureRequestDto?>(null);

        public Task<ErasureRequestDto?> RejectAsync(Guid id, string rejectionReason, CancellationToken cancellationToken = default)
            => Task.FromResult<ErasureRequestDto?>(null);

        public Task<int> ExecuteAnonymizationAsync(Guid id, CancellationToken cancellationToken = default)
            => Task.FromResult(1);
    }

    private sealed class StubDisasterRecoveryService : IDisasterRecoveryService
    {
        public Task<DRDrillDto> TriggerDrillAsync(DisasterScenario scenario, CancellationToken cancellationToken = default)
            => Task.FromResult(new DRDrillDto(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow,
                scenario.ToString().ToUpperInvariant(),
                TimeSpan.FromMinutes(5),
                "COMPLETED",
                "ok",
                Array.Empty<string>()));

        public Task<IReadOnlyList<DRDrillDto>> GetHistoryAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<DRDrillDto>>(Array.Empty<DRDrillDto>());

        public Task<DRDrillDto> RunQuarterlyDrillAsync(CancellationToken cancellationToken = default)
            => TriggerDrillAsync(DisasterScenario.DataCenterOutage, cancellationToken);
    }
}

