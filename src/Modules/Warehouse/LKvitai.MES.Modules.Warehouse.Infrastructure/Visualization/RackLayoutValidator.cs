using System.Text.Json;
using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Visualization;

public sealed class RackLayoutValidator
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly HashSet<string> AllowedRackTypes =
    [
        "PalletRack",
        "FloorStorage",
        "WallShelf",
        "Custom"
    ];

    public RackLayoutDocument Parse(string? racksJson)
    {
        if (string.IsNullOrWhiteSpace(racksJson))
        {
            return RackLayoutDocument.Empty;
        }

        return JsonSerializer.Deserialize<RackLayoutDocument>(racksJson, JsonOptions)
            ?? RackLayoutDocument.Empty;
    }

    public RackLayoutValidationResult Validate(WarehouseLayout layout, RackLayoutDocument document)
    {
        var errors = new List<string>();
        var racks = document.GetRacks();

        var duplicateRackIds = racks
            .GroupBy(x => x.Id, StringComparer.OrdinalIgnoreCase)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var rackId in duplicateRackIds)
        {
            errors.Add($"Rack id '{rackId}' must be unique within the warehouse.");
        }

        foreach (var rack in racks)
        {
            ValidateRack(layout, rack, errors);
        }

        ValidatePairings(racks, errors);
        ValidateOverlap(racks, errors);

        return new RackLayoutValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateRack(WarehouseLayout layout, RackRowDefinition rack, ICollection<string> errors)
    {
        if (string.IsNullOrWhiteSpace(rack.Id))
        {
            errors.Add("Each rack requires a non-empty id.");
        }

        if (!AllowedRackTypes.Contains(rack.Type))
        {
            errors.Add($"Rack '{rack.Id}' has unsupported type '{rack.Type}'.");
        }

        if (rack.Dimensions.Width <= 0 || rack.Dimensions.Depth <= 0 || rack.Dimensions.Height <= 0)
        {
            errors.Add($"Rack '{rack.Id}' dimensions must all be greater than zero.");
        }

        if (IsSlotBased(rack.Type))
        {
            if (rack.SlotsPerLevel < 1)
            {
                errors.Add($"Rack '{rack.Id}' must define slotsPerLevel >= 1.");
            }

            var levels = rack.GetLevels()
                .OrderBy(x => x.Index)
                .ToList();

            if (levels.Count == 0)
            {
                errors.Add($"Rack '{rack.Id}' must define at least one level.");
            }

            ValidateLevels(rack, levels, errors);
        }
        else if (string.Equals(rack.Type, "FloorStorage", StringComparison.OrdinalIgnoreCase))
        {
            if (rack.SlotsPerLevel != 0)
            {
                errors.Add($"Rack '{rack.Id}' must define slotsPerLevel = 0 for FloorStorage.");
            }

            if (rack.GetLevels().Count > 0)
            {
                errors.Add($"Rack '{rack.Id}' must not define levels for FloorStorage.");
            }
        }

        if (rack.BayCount < 0)
        {
            errors.Add($"Rack '{rack.Id}' must define bayCount >= 0.");
        }

        var footprint = ComputeFootprint(rack);
        if (footprint.MinX < 0 || footprint.MinY < 0 || rack.Origin.Z < 0)
        {
            errors.Add($"Rack '{rack.Id}' must stay within non-negative warehouse bounds.");
        }

        if (footprint.MaxX > layout.WidthMeters || footprint.MaxY > layout.LengthMeters || rack.Origin.Z + rack.Dimensions.Height > layout.HeightMeters)
        {
            errors.Add($"Rack '{rack.Id}' exceeds the warehouse bounds.");
        }
    }

    private static void ValidateLevels(
        RackRowDefinition rack,
        IReadOnlyList<RackLevelDefinition> levels,
        ICollection<string> errors)
    {
        var duplicateIndexes = levels
            .GroupBy(x => x.Index)
            .Where(x => x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        foreach (var duplicateIndex in duplicateIndexes)
        {
            errors.Add($"Rack '{rack.Id}' level index '{duplicateIndex}' must be unique.");
        }

        for (var i = 0; i < levels.Count; i++)
        {
            var level = levels[i];
            var expectedIndex = i + 1;
            if (level.Index != expectedIndex)
            {
                errors.Add($"Rack '{rack.Id}' levels must use contiguous indexes starting at 1.");
                break;
            }

            if (level.HeightFromBase < 0 || level.HeightFromBase >= rack.Dimensions.Height)
            {
                errors.Add($"Rack '{rack.Id}' level '{level.Index}' has invalid heightFromBase.");
            }

            if (i > 0 && levels[i - 1].HeightFromBase >= level.HeightFromBase)
            {
                errors.Add($"Rack '{rack.Id}' levels must be in strictly ascending heightFromBase order.");
            }
        }
    }

    private static void ValidatePairings(
        IReadOnlyList<RackRowDefinition> racks,
        ICollection<string> errors)
    {
        var rackIds = new HashSet<string>(racks.Select(x => x.Id), StringComparer.OrdinalIgnoreCase);
        foreach (var rack in racks)
        {
            if (!string.IsNullOrWhiteSpace(rack.PairedWithRackId) && !rackIds.Contains(rack.PairedWithRackId))
            {
                errors.Add($"Rack '{rack.Id}' references missing pairedWithRackId '{rack.PairedWithRackId}'.");
            }
        }
    }

    private static void ValidateOverlap(
        IReadOnlyList<RackRowDefinition> racks,
        ICollection<string> errors)
    {
        for (var i = 0; i < racks.Count - 1; i++)
        {
            var left = ComputeFootprint(racks[i]);
            for (var j = i + 1; j < racks.Count; j++)
            {
                var right = ComputeFootprint(racks[j]);
                var overlaps =
                    left.MinX < right.MaxX &&
                    left.MaxX > right.MinX &&
                    left.MinY < right.MaxY &&
                    left.MaxY > right.MinY;

                if (overlaps)
                {
                    errors.Add($"Racks '{racks[i].Id}' and '{racks[j].Id}' overlap within the warehouse footprint.");
                }
            }
        }
    }

    public static bool IsSlotBased(string rackType)
        => string.Equals(rackType, "PalletRack", StringComparison.OrdinalIgnoreCase) ||
           string.Equals(rackType, "WallShelf", StringComparison.OrdinalIgnoreCase);

    public static (decimal MinX, decimal MinY, decimal MaxX, decimal MaxY) ComputeFootprint(RackRowDefinition rack)
    {
        var radians = (double)(rack.OrientationDeg * (decimal)Math.PI / 180m);
        var cos = (decimal)Math.Cos(radians);
        var sin = (decimal)Math.Sin(radians);

        static (decimal X, decimal Y) Rotate(decimal localX, decimal localY, RackOriginDefinition origin, decimal cos, decimal sin)
            => (origin.X + (localX * cos) - (localY * sin), origin.Y + (localX * sin) + (localY * cos));

        var points = new[]
        {
            Rotate(0m, 0m, rack.Origin, cos, sin),
            Rotate(rack.Dimensions.Width, 0m, rack.Origin, cos, sin),
            Rotate(0m, rack.Dimensions.Depth, rack.Origin, cos, sin),
            Rotate(rack.Dimensions.Width, rack.Dimensions.Depth, rack.Origin, cos, sin)
        };

        return (
            points.Min(x => x.X),
            points.Min(x => x.Y),
            points.Max(x => x.X),
            points.Max(x => x.Y));
    }
}
