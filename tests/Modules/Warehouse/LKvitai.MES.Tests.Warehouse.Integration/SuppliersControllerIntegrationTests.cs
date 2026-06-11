using FluentAssertions;
using LKvitai.MES.Modules.Warehouse.Api.Controllers;
using LKvitai.MES.Modules.Warehouse.Application.Services;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace LKvitai.MES.Tests.Warehouse.Integration;

public class SuppliersControllerIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private DbContextOptions<WarehouseDbContext>? _options;

    public async Task InitializeAsync()
    {
        if (!DockerRequirement.IsEnabled)
        {
            return;
        }

        _postgres = new PostgreSqlBuilder()
            .WithImage("pgvector/pgvector:pg16")
            .Build();
        await _postgres.StartAsync();

        await using (var conn = new Npgsql.NpgsqlConnection(_postgres.GetConnectionString()))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "CREATE EXTENSION IF NOT EXISTS vector;";
            await cmd.ExecuteNonQueryAsync();
        }

        _options = new DbContextOptionsBuilder<WarehouseDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options;
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
        {
            await _postgres.DisposeAsync();
        }
    }

    [SkippableFact]
    public async Task Create_Then_List_ShouldReturnAllStructuredFields()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var request = new SuppliersController.UpsertSupplierRequest(
            Code: "SUP-1",
            Name: "ABC Fasteners",
            ShortName: "ABC",
            CompanyCode: "302345678",
            VatCode: "LT100001234567",
            RegisteredAddress: "Gamyklos g. 1, Vilnius",
            PickupAddress: "Sandelio g. 5, Vilnius",
            City: "Vilnius",
            Country: "Lithuania",
            ContactName: "Jonas Jonaitis",
            Phone: "+37060000000",
            Email: "orders@abc.example",
            Website: "https://abc.example",
            AdditionalInfo: "Preferred",
            ContactInfo: "legacy blob");

        var created = await controller.CreateAsync(request);
        created.Should().BeOfType<CreatedResult>();

        var list = await controller.GetAsync(search: null, country: null);
        var ok = list.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<SuppliersController.PagedResponse<SuppliersController.SupplierListItemDto>>().Subject;

        var dto = payload.Items.Should().ContainSingle().Subject;
        dto.Code.Should().Be("SUP-1");
        dto.Name.Should().Be("ABC Fasteners");
        dto.ShortName.Should().Be("ABC");
        dto.CompanyCode.Should().Be("302345678");
        dto.VatCode.Should().Be("LT100001234567");
        dto.RegisteredAddress.Should().Be("Gamyklos g. 1, Vilnius");
        dto.PickupAddress.Should().Be("Sandelio g. 5, Vilnius");
        dto.City.Should().Be("Vilnius");
        dto.Country.Should().Be("Lithuania");
        dto.ContactName.Should().Be("Jonas Jonaitis");
        dto.Phone.Should().Be("+37060000000");
        dto.Email.Should().Be("orders@abc.example");
        dto.Website.Should().Be("https://abc.example");
        dto.AdditionalInfo.Should().Be("Preferred");
        dto.ContactInfo.Should().Be("legacy blob");
    }

    [SkippableFact]
    public async Task Update_ShouldPreserveAllStructuredFields_AndKeepAgnumClientId()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext();
        db.Suppliers.Add(new Supplier
        {
            AgnumClientId = 57,
            Code = "SUP-2",
            Name = "Old Name",
            CompanyCode = "111"
        });
        await db.SaveChangesAsync();
        var id = await db.Suppliers.Where(x => x.Code == "SUP-2").Select(x => x.Id).SingleAsync();

        var controller = CreateController(db);
        var request = new SuppliersController.UpsertSupplierRequest(
            Code: "SUP-2",
            Name: "New Name",
            ShortName: "NN",
            CompanyCode: "222",
            VatCode: "LT222",
            City: "Kaunas",
            Country: "Lithuania",
            Phone: "+3705",
            Email: "new@example.com");

        var result = await controller.UpdateAsync(id, request);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = ok.Value.Should().BeOfType<SuppliersController.SupplierListItemDto>().Subject;

        dto.Name.Should().Be("New Name");
        dto.ShortName.Should().Be("NN");
        dto.CompanyCode.Should().Be("222");
        dto.VatCode.Should().Be("LT222");
        dto.City.Should().Be("Kaunas");
        dto.Phone.Should().Be("+3705");
        dto.Email.Should().Be("new@example.com");
        dto.AgnumClientId.Should().Be(57);

        var persisted = await db.Suppliers.AsNoTracking().SingleAsync(x => x.Id == id);
        persisted.AgnumClientId.Should().Be(57);
        persisted.VatCode.Should().Be("LT222");
    }

    [SkippableFact]
    public async Task DuplicateCode_ShouldReturnValidationError()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext();
        db.Suppliers.Add(new Supplier { Code = "DUP", Name = "Existing" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.CreateAsync(new SuppliersController.UpsertSupplierRequest("DUP", "Another"));

        var objectResult = result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
        (await db.Suppliers.CountAsync()).Should().Be(1);
    }

    [SkippableTheory]
    [InlineData("302345678")] // company code
    [InlineData("LT100001234567")] // vat
    [InlineData("orders@abc.example")] // email
    [InlineData("+37060000000")] // phone
    [InlineData("Vilnius")] // city
    public async Task Search_ShouldMatchAcrossStructuredFields(string term)
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext();
        db.Suppliers.Add(new Supplier
        {
            Code = "SUP-3",
            Name = "ABC Fasteners",
            CompanyCode = "302345678",
            VatCode = "LT100001234567",
            Email = "orders@abc.example",
            Phone = "+37060000000",
            City = "Vilnius",
            Country = "Lithuania"
        });
        db.Suppliers.Add(new Supplier { Code = "ZZZ-9", Name = "Unrelated" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetAsync(search: term, country: null);
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<SuppliersController.PagedResponse<SuppliersController.SupplierListItemDto>>().Subject;

        payload.Items.Should().ContainSingle(x => x.Code == "SUP-3");
    }

    [SkippableFact]
    public async Task CountryFilter_ShouldRestrictResults()
    {
        DockerRequirement.EnsureEnabled();
        await ResetDatabaseAsync();

        await using var db = CreateDbContext();
        db.Suppliers.Add(new Supplier { Code = "LT-1", Name = "Lithuanian", Country = "Lithuania" });
        db.Suppliers.Add(new Supplier { Code = "PL-1", Name = "Polish", Country = "Poland" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var result = await controller.GetAsync(search: null, country: "Poland");
        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var payload = ok.Value.Should().BeOfType<SuppliersController.PagedResponse<SuppliersController.SupplierListItemDto>>().Subject;

        payload.Items.Should().ContainSingle(x => x.Code == "PL-1");
    }

    private WarehouseDbContext CreateDbContext()
        => new(_options!, new StaticCurrentUserService("supplier-admin"));

    private SuppliersController CreateController(WarehouseDbContext db)
    {
        var controller = new SuppliersController(db);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private async Task ResetDatabaseAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        db.Suppliers.RemoveRange(db.Suppliers);
        await db.SaveChangesAsync();
    }

    private sealed class StaticCurrentUserService : ICurrentUserService
    {
        private readonly string _userId;

        public StaticCurrentUserService(string userId)
        {
            _userId = userId;
        }

        public string GetCurrentUserId() => _userId;
    }
}
