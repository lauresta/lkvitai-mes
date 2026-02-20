using LKvitai.MES.Modules.Warehouse.Api.ErrorHandling;
using LKvitai.MES.Modules.Warehouse.Api.Security;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Caching;
using LKvitai.MES.Modules.Warehouse.Infrastructure.Persistence;
using LKvitai.MES.SharedKernel;
using CsvHelper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;

namespace LKvitai.MES.Modules.Warehouse.Api.Controllers;

[ApiController]
[Route("api/warehouse/v1/locations")]
public sealed class LocationsController : ControllerBase
{
    private static readonly string[] AllowedZoneTypes = ["General", "Refrigerated", "Hazmat", "Quarantine"];
    private static readonly Dictionary<string, string> CanonicalZoneTypes = AllowedZoneTypes
        .ToDictionary(static value => value, static value => value, StringComparer.OrdinalIgnoreCase);

    private readonly WarehouseDbContext _dbContext;

    public LocationsController(WarehouseDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    [Authorize(Policy = WarehousePolicies.OperatorOrAbove)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string? search,
        [FromQuery] string? status,
        [FromQuery] bool includeVirtual = true,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 500);

        var query = _dbContext.Locations
            .AsNoTracking()
            .AsQueryable();

        if (!includeVirtual)
        {
            query = query.Where(x => !x.IsVirtual);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(x =>
                x.Code.ToLower().Contains(normalized) ||
                x.Barcode.ToLower().Contains(normalized));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim();
            query = query.Where(x => x.Status == normalizedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var rows = await query
            .OrderBy(x => x.Code)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new LocationListItemDto(
                x.Id,
                x.Code,
                x.Barcode,
                x.Type,
                x.ParentLocationId,
                x.IsVirtual,
                x.MaxWeight,
                x.MaxVolume,
                x.Status,
                x.ZoneType,
                x.CoordinateX,
                x.CoordinateY,
                x.CoordinateZ,
                x.WidthMeters,
                x.LengthMeters,
                x.HeightMeters,
                x.Aisle,
                x.Rack,
                x.Level,
                x.Bin,
                x.CapacityWeight,
                x.CapacityVolume,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResponse<LocationListItemDto>(rows, totalCount, pageNumber, pageSize));
    }

    [HttpPost]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> CreateAsync(
        [FromBody] UpsertLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationFailure("Field 'code' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            return ValidationFailure("Field 'barcode' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return ValidationFailure("Field 'type' is required.");
        }

        if (request.ParentLocationId.HasValue)
        {
            var parentExists = await _dbContext.Locations
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.ParentLocationId.Value, cancellationToken);
            if (!parentExists)
            {
                return ValidationFailure($"Parent location '{request.ParentLocationId.Value}' does not exist.");
            }
        }

        if (!TryNormalizeZoneType(request.ZoneType, out var normalizedZoneType, out var zoneTypeValidationError))
        {
            return ValidationFailure(zoneTypeValidationError!);
        }

        if (!TryValidateCoordinates(
                request.CoordinateX,
                request.CoordinateY,
                request.CoordinateZ,
                out var coordinateValidationError))
        {
            return ValidationFailure(coordinateValidationError!);
        }

        if (!TryNormalizeBinDimensions(
                request.WidthMeters,
                request.LengthMeters,
                request.HeightMeters,
                out var normalizedWidthMeters,
                out var normalizedLengthMeters,
                out var normalizedHeightMeters,
                out var dimensionsValidationError))
        {
            return ValidationFailure(dimensionsValidationError!);
        }

        var normalizedCode = request.Code.Trim();
        var normalizedBarcode = request.Barcode.Trim();

        var duplicateCode = await _dbContext.Locations
            .AsNoTracking()
            .AnyAsync(x => x.Code == normalizedCode, cancellationToken);
        if (duplicateCode)
        {
            return ValidationFailure($"Location code '{normalizedCode}' already exists.");
        }

        var duplicateBarcode = await _dbContext.Locations
            .AsNoTracking()
            .AnyAsync(x => x.Barcode == normalizedBarcode, cancellationToken);
        if (duplicateBarcode)
        {
            return ValidationFailure($"Location barcode '{normalizedBarcode}' already exists.");
        }

        var entity = new Location
        {
            Code = normalizedCode,
            Barcode = normalizedBarcode,
            Type = request.Type.Trim(),
            ParentLocationId = request.ParentLocationId,
            IsVirtual = request.IsVirtual,
            MaxWeight = request.MaxWeight,
            MaxVolume = request.MaxVolume,
            Status = string.IsNullOrWhiteSpace(request.Status) ? "Active" : request.Status.Trim(),
            ZoneType = normalizedZoneType,
            CoordinateX = request.CoordinateX,
            CoordinateY = request.CoordinateY,
            CoordinateZ = request.CoordinateZ,
            WidthMeters = normalizedWidthMeters,
            LengthMeters = normalizedLengthMeters,
            HeightMeters = normalizedHeightMeters,
            Aisle = request.Aisle?.Trim(),
            Rack = request.Rack?.Trim(),
            Level = request.Level?.Trim(),
            Bin = request.Bin?.Trim(),
            CapacityWeight = request.CapacityWeight,
            CapacityVolume = request.CapacityVolume
        };

        var overlapError = await ValidateCoordinateOverlapAsync(entity, null, cancellationToken);
        if (overlapError is not null)
        {
            return ValidationFailure(overlapError);
        }

        _dbContext.Locations.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"location:{entity.Code}", cancellationToken);

        return Created($"/api/warehouse/v1/locations/{entity.Id}", new LocationListItemDto(
            entity.Id,
            entity.Code,
            entity.Barcode,
            entity.Type,
            entity.ParentLocationId,
            entity.IsVirtual,
            entity.MaxWeight,
            entity.MaxVolume,
            entity.Status,
            entity.ZoneType,
            entity.CoordinateX,
            entity.CoordinateY,
            entity.CoordinateZ,
            entity.WidthMeters,
            entity.LengthMeters,
            entity.HeightMeters,
            entity.Aisle,
            entity.Rack,
            entity.Level,
            entity.Bin,
            entity.CapacityWeight,
            entity.CapacityVolume,
            entity.CreatedAt,
            entity.UpdatedAt));
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateAsync(
        int id,
        [FromBody] UpsertLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return ValidationFailure("Field 'code' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Barcode))
        {
            return ValidationFailure("Field 'barcode' is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Type))
        {
            return ValidationFailure("Field 'type' is required.");
        }

        var entity = await _dbContext.Locations.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Location '{id}' does not exist."));
        }

        if (request.ParentLocationId == id)
        {
            return ValidationFailure("Location cannot reference itself as parent.");
        }

        if (request.ParentLocationId.HasValue)
        {
            var parentExists = await _dbContext.Locations
                .AsNoTracking()
                .AnyAsync(x => x.Id == request.ParentLocationId.Value, cancellationToken);
            if (!parentExists)
            {
                return ValidationFailure($"Parent location '{request.ParentLocationId.Value}' does not exist.");
            }
        }

        if (!TryNormalizeZoneType(request.ZoneType, out var normalizedZoneType, out var zoneTypeValidationError))
        {
            return ValidationFailure(zoneTypeValidationError!);
        }

        if (!TryValidateCoordinates(
                request.CoordinateX,
                request.CoordinateY,
                request.CoordinateZ,
                out var coordinateValidationError))
        {
            return ValidationFailure(coordinateValidationError!);
        }

        if (!TryNormalizeBinDimensions(
                request.WidthMeters,
                request.LengthMeters,
                request.HeightMeters,
                out var normalizedWidthMeters,
                out var normalizedLengthMeters,
                out var normalizedHeightMeters,
                out var dimensionsValidationError))
        {
            return ValidationFailure(dimensionsValidationError!);
        }

        var normalizedCode = request.Code.Trim();
        var normalizedBarcode = request.Barcode.Trim();

        if (entity.IsVirtual &&
            (!string.Equals(entity.Code, normalizedCode, StringComparison.Ordinal) ||
             !string.Equals(entity.Barcode, normalizedBarcode, StringComparison.Ordinal)))
        {
            return UnprocessableFailure("Virtual locations cannot modify Code or Barcode.");
        }

        var duplicateCode = await _dbContext.Locations
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Code == normalizedCode, cancellationToken);
        if (duplicateCode)
        {
            return ValidationFailure($"Location code '{normalizedCode}' already exists.");
        }

        var duplicateBarcode = await _dbContext.Locations
            .AsNoTracking()
            .AnyAsync(x => x.Id != id && x.Barcode == normalizedBarcode, cancellationToken);
        if (duplicateBarcode)
        {
            return ValidationFailure($"Location barcode '{normalizedBarcode}' already exists.");
        }

        var previousCode = entity.Code;
        entity.Code = normalizedCode;
        entity.Barcode = normalizedBarcode;
        entity.Type = request.Type.Trim();
        entity.ParentLocationId = request.ParentLocationId;
        entity.MaxWeight = request.MaxWeight;
        entity.MaxVolume = request.MaxVolume;
        entity.Status = string.IsNullOrWhiteSpace(request.Status) ? entity.Status : request.Status.Trim();
        entity.ZoneType = normalizedZoneType;
        entity.CoordinateX = request.CoordinateX;
        entity.CoordinateY = request.CoordinateY;
        entity.CoordinateZ = request.CoordinateZ;
        entity.WidthMeters = normalizedWidthMeters;
        entity.LengthMeters = normalizedLengthMeters;
        entity.HeightMeters = normalizedHeightMeters;
        entity.Aisle = request.Aisle?.Trim();
        entity.Rack = request.Rack?.Trim();
        entity.Level = request.Level?.Trim();
        entity.Bin = request.Bin?.Trim();
        entity.CapacityWeight = request.CapacityWeight;
        entity.CapacityVolume = request.CapacityVolume;

        var overlapError = await ValidateCoordinateOverlapAsync(entity, entity.Id, cancellationToken);
        if (overlapError is not null)
        {
            return ValidationFailure(overlapError);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await Cache.RemoveAsync($"location:{previousCode}", cancellationToken);
        await Cache.RemoveAsync($"location:{entity.Code}", cancellationToken);
        return Ok(new LocationListItemDto(
            entity.Id,
            entity.Code,
            entity.Barcode,
            entity.Type,
            entity.ParentLocationId,
            entity.IsVirtual,
            entity.MaxWeight,
            entity.MaxVolume,
            entity.Status,
            entity.ZoneType,
            entity.CoordinateX,
            entity.CoordinateY,
            entity.CoordinateZ,
            entity.WidthMeters,
            entity.LengthMeters,
            entity.HeightMeters,
            entity.Aisle,
            entity.Rack,
            entity.Level,
            entity.Bin,
            entity.CapacityWeight,
            entity.CapacityVolume,
            entity.CreatedAt,
            entity.UpdatedAt));
    }

    [HttpPut("{code:regex(^\\D.*$)}")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    public async Task<IActionResult> UpdateByCodeAsync(
        string code,
        [FromBody] UpdateCoordinatesRequest request,
        CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return ValidationFailure("Location code is required.");
        }

        var entity = await _dbContext.Locations.FirstOrDefaultAsync(
            x => x.Code == normalizedCode,
            cancellationToken);
        if (entity is null)
        {
            return Failure(Result.Fail(DomainErrorCodes.NotFound, $"Location '{normalizedCode}' does not exist."));
        }

        if (!TryNormalizeBinDimensions(
                request.WidthMeters,
                request.LengthMeters,
                request.HeightMeters,
                out var normalizedWidthMeters,
                out var normalizedLengthMeters,
                out var normalizedHeightMeters,
                out var dimensionsValidationError))
        {
            return ValidationFailure(dimensionsValidationError!);
        }

        if (!TryValidateCoordinates(
                request.CoordinateX,
                request.CoordinateY,
                request.CoordinateZ,
                out var coordinateValidationError))
        {
            return ValidationFailure(coordinateValidationError!);
        }

        entity.CoordinateX = request.CoordinateX;
        entity.CoordinateY = request.CoordinateY;
        entity.CoordinateZ = request.CoordinateZ;
        entity.WidthMeters = normalizedWidthMeters ?? entity.WidthMeters;
        entity.LengthMeters = normalizedLengthMeters ?? entity.LengthMeters;
        entity.HeightMeters = normalizedHeightMeters ?? entity.HeightMeters;
        entity.Aisle = request.Aisle?.Trim();
        entity.Rack = request.Rack?.Trim();
        entity.Level = request.Level?.Trim();
        entity.Bin = request.Bin?.Trim();
        entity.CapacityWeight = request.CapacityWeight ?? entity.CapacityWeight;
        entity.CapacityVolume = request.CapacityVolume ?? entity.CapacityVolume;

        var overlapError = await ValidateCoordinateOverlapAsync(entity, entity.Id, cancellationToken);
        if (overlapError is not null)
        {
            return ValidationFailure(overlapError);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new LocationCoordinateResponse(
            entity.Id,
            entity.Code,
            entity.CoordinateX,
            entity.CoordinateY,
            entity.CoordinateZ,
            entity.WidthMeters,
            entity.LengthMeters,
            entity.HeightMeters,
            entity.Aisle,
            entity.Rack,
            entity.Level,
            entity.Bin,
            entity.CapacityWeight,
            entity.CapacityVolume));
    }

    [HttpPost("bulk-coordinates")]
    [Authorize(Policy = WarehousePolicies.ManagerOrAdmin)]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> BulkCoordinatesAsync(
        [FromForm] IFormFile? file,
        CancellationToken cancellationToken = default)
    {
        if (file is null || file.Length == 0)
        {
            return ValidationFailure("CSV file is required.");
        }

        var rows = new List<BulkCoordinateCsvRow>();
        await using (var stream = file.OpenReadStream())
        using (var reader = new StreamReader(stream))
        using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
        {
            if (!await csv.ReadAsync())
            {
                return ValidationFailure("CSV is empty.");
            }

            csv.ReadHeader();
            while (await csv.ReadAsync())
            {
                rows.Add(new BulkCoordinateCsvRow(
                    csv.GetField("LocationCode") ?? string.Empty,
                    csv.GetField("X"),
                    csv.GetField("Y"),
                    csv.GetField("Z"),
                    csv.GetField("CapacityWeight"),
                    csv.GetField("CapacityVolume")));
            }
        }

        if (rows.Count == 0)
        {
            return ValidationFailure("CSV does not contain data rows.");
        }

        var codes = rows
            .Select(x => x.LocationCode.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var locations = await _dbContext.Locations
            .Where(x => codes.Contains(x.Code))
            .ToDictionaryAsync(x => x.Code, x => x, StringComparer.OrdinalIgnoreCase, cancellationToken);

        var errors = new List<string>();
        var successCount = 0;
        var coordinateSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var locationCode = row.LocationCode.Trim();
            if (string.IsNullOrWhiteSpace(locationCode))
            {
                errors.Add("LocationCode is required.");
                continue;
            }

            if (!locations.TryGetValue(locationCode, out var location))
            {
                errors.Add($"Location '{locationCode}' not found.");
                continue;
            }

            if (!TryParseRequiredDecimal(row.X, out var x) ||
                !TryParseRequiredDecimal(row.Y, out var y) ||
                !TryParseRequiredDecimal(row.Z, out var z))
            {
                errors.Add($"Location '{locationCode}': invalid X/Y/Z values.");
                continue;
            }

            if (!TryValidateCoordinates(x, y, z, out var coordinateValidationError))
            {
                errors.Add($"Location '{locationCode}': {coordinateValidationError}");
                continue;
            }

            if (!TryParseOptionalDecimal(row.CapacityWeight, out var capacityWeight) ||
                !TryParseOptionalDecimal(row.CapacityVolume, out var capacityVolume))
            {
                errors.Add($"Location '{locationCode}': invalid CapacityWeight or CapacityVolume.");
                continue;
            }

            var coordinateKey = BuildCoordinateKey(x, y, z);
            if (!coordinateSet.Add(coordinateKey))
            {
                errors.Add($"Location '{locationCode}': coordinate overlap detected in upload batch.");
                continue;
            }

            location.CoordinateX = x;
            location.CoordinateY = y;
            location.CoordinateZ = z;
            location.CapacityWeight = capacityWeight ?? location.CapacityWeight;
            location.CapacityVolume = capacityVolume ?? location.CapacityVolume;

            var overlapError = await ValidateCoordinateOverlapAsync(location, location.Id, cancellationToken);
            if (overlapError is not null)
            {
                errors.Add($"Location '{locationCode}': {overlapError}");
                continue;
            }

            successCount++;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Ok(new BulkCoordinatesResponse(successCount, errors.Count, errors));
    }

    private ObjectResult ValidationFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status400BadRequest
        };
    }

    private ObjectResult UnprocessableFailure(string detail)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(
            DomainErrorCodes.ValidationError,
            detail,
            HttpContext);

        return new ObjectResult(problemDetails)
        {
            StatusCode = StatusCodes.Status422UnprocessableEntity
        };
    }

    private ObjectResult Failure(Result result)
    {
        var problemDetails = ResultProblemDetailsMapper.ToProblemDetails(result, HttpContext);
        return new ObjectResult(problemDetails)
        {
            StatusCode = problemDetails.Status
        };
    }

    private async Task<string?> ValidateCoordinateOverlapAsync(
        Location location,
        int? currentLocationId,
        CancellationToken cancellationToken)
    {
        if (!location.CoordinateX.HasValue ||
            !location.CoordinateY.HasValue ||
            !location.CoordinateZ.HasValue)
        {
            return null;
        }

        var x = location.CoordinateX.Value;
        var y = location.CoordinateY.Value;
        var z = location.CoordinateZ.Value;

        var conflict = await _dbContext.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                candidate =>
                    (!currentLocationId.HasValue || candidate.Id != currentLocationId.Value) &&
                    candidate.CoordinateX == x &&
                    candidate.CoordinateY == y &&
                    candidate.CoordinateZ == z,
                cancellationToken);

        return conflict is null
            ? null
            : $"Location coordinate overlap detected with '{conflict.Code}'.";
    }

    private static bool TryNormalizeZoneType(
        string? rawZoneType,
        out string? normalizedZoneType,
        out string? validationError)
    {
        validationError = null;
        normalizedZoneType = null;

        if (string.IsNullOrWhiteSpace(rawZoneType))
        {
            return true;
        }

        var trimmed = rawZoneType.Trim();
        if (!CanonicalZoneTypes.TryGetValue(trimmed, out var canonical))
        {
            validationError = $"Field 'zoneType' must be one of: {string.Join(", ", AllowedZoneTypes)}.";
            return false;
        }

        normalizedZoneType = canonical;
        return true;
    }

    private static bool TryValidateCoordinates(
        decimal? x,
        decimal? y,
        decimal? z,
        out string? validationError)
    {
        validationError = null;

        if ((x.HasValue && x.Value < 0m) ||
            (y.HasValue && y.Value < 0m) ||
            (z.HasValue && z.Value < 0m))
        {
            validationError = "Coordinates must be >= 0.";
            return false;
        }

        return true;
    }

    private static bool TryParseRequiredDecimal(string? raw, out decimal value)
    {
        value = 0m;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        return decimal.TryParse(raw.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out value);
    }

    private static bool TryParseOptionalDecimal(string? raw, out decimal? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!decimal.TryParse(raw.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        value = parsed;
        return true;
    }

    private static string BuildCoordinateKey(decimal x, decimal y, decimal z)
    {
        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:0.##}|{1:0.##}|{2:0.##}",
            x,
            y,
            z);
    }

    private static bool TryNormalizeBinDimensions(
        decimal? rawWidthMeters,
        decimal? rawLengthMeters,
        decimal? rawHeightMeters,
        out decimal? normalizedWidthMeters,
        out decimal? normalizedLengthMeters,
        out decimal? normalizedHeightMeters,
        out string? validationError)
    {
        normalizedWidthMeters = null;
        normalizedLengthMeters = null;
        normalizedHeightMeters = null;
        validationError = null;

        var hasWidth = rawWidthMeters.HasValue;
        var hasLength = rawLengthMeters.HasValue;
        var hasHeight = rawHeightMeters.HasValue;

        if (!hasWidth && !hasLength && !hasHeight)
        {
            return true;
        }

        if (!hasWidth || !hasLength || !hasHeight)
        {
            validationError = "widthMeters, lengthMeters, and heightMeters must be provided together.";
            return false;
        }

        if (rawWidthMeters <= 0m || rawLengthMeters <= 0m || rawHeightMeters <= 0m)
        {
            validationError = "widthMeters, lengthMeters, and heightMeters must be greater than zero.";
            return false;
        }

        normalizedWidthMeters = decimal.Round(rawWidthMeters!.Value, 3, MidpointRounding.AwayFromZero);
        normalizedLengthMeters = decimal.Round(rawLengthMeters!.Value, 3, MidpointRounding.AwayFromZero);
        normalizedHeightMeters = decimal.Round(rawHeightMeters!.Value, 3, MidpointRounding.AwayFromZero);
        return true;
    }

    private ICacheService Cache => HttpContext?.RequestServices?.GetService<ICacheService>() ?? new LKvitai.MES.Modules.Warehouse.Infrastructure.Caching.NoOpCacheService();

    public sealed record UpsertLocationRequest(
        string Code,
        string Barcode,
        string Type,
        int? ParentLocationId,
        bool IsVirtual,
        decimal? MaxWeight,
        decimal? MaxVolume,
        string? Status,
        string? ZoneType,
        decimal? CoordinateX,
        decimal? CoordinateY,
        decimal? CoordinateZ,
        decimal? WidthMeters,
        decimal? LengthMeters,
        decimal? HeightMeters,
        string? Aisle,
        string? Rack,
        string? Level,
        string? Bin,
        decimal? CapacityWeight,
        decimal? CapacityVolume);

    public sealed record UpdateCoordinatesRequest(
        decimal? CoordinateX,
        decimal? CoordinateY,
        decimal? CoordinateZ,
        decimal? WidthMeters,
        decimal? LengthMeters,
        decimal? HeightMeters,
        string? Aisle,
        string? Rack,
        string? Level,
        string? Bin,
        decimal? CapacityWeight,
        decimal? CapacityVolume);

    public sealed record LocationListItemDto(
        int Id,
        string Code,
        string Barcode,
        string Type,
        int? ParentLocationId,
        bool IsVirtual,
        decimal? MaxWeight,
        decimal? MaxVolume,
        string Status,
        string? ZoneType,
        decimal? CoordinateX,
        decimal? CoordinateY,
        decimal? CoordinateZ,
        decimal? WidthMeters,
        decimal? LengthMeters,
        decimal? HeightMeters,
        string? Aisle,
        string? Rack,
        string? Level,
        string? Bin,
        decimal? CapacityWeight,
        decimal? CapacityVolume,
        DateTimeOffset CreatedAt,
        DateTimeOffset? UpdatedAt);

    public sealed record LocationCoordinateResponse(
        int Id,
        string Code,
        decimal? CoordinateX,
        decimal? CoordinateY,
        decimal? CoordinateZ,
        decimal? WidthMeters,
        decimal? LengthMeters,
        decimal? HeightMeters,
        string? Aisle,
        string? Rack,
        string? Level,
        string? Bin,
        decimal? CapacityWeight,
        decimal? CapacityVolume);

    public sealed record BulkCoordinatesResponse(
        int SuccessCount,
        int ErrorCount,
        IReadOnlyList<string> Errors);

    private sealed record BulkCoordinateCsvRow(
        string LocationCode,
        string? X,
        string? Y,
        string? Z,
        string? CapacityWeight,
        string? CapacityVolume);

    public sealed record PagedResponse<T>(
        IReadOnlyList<T> Items,
        int TotalCount,
        int PageNumber,
        int PageSize);
}
