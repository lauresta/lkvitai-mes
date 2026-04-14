using LKvitai.MES.Modules.Warehouse.Domain.Entities;

namespace LKvitai.MES.Modules.Warehouse.Infrastructure.Visualization;

public sealed class WarehouseGeometryCalculator
{
    private const decimal Inset = 0.04m;

    public WarehouseGeometryResult Calculate(
        string warehouseCode,
        RackLayoutDocument rackLayout,
        IReadOnlyList<Location> locations)
    {
        var racks = rackLayout.GetRacks();
        var rackById = racks.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        var occupiedSlots = new HashSet<(string RackId, int Level, int Slot)>();
        var bins = new List<VisualizationBinModel>(locations.Count);

        foreach (var location in locations)
        {
            if (TryCreateRackBin(warehouseCode, location, rackById, occupiedSlots, out var rackBin))
            {
                bins.Add(rackBin!);
                continue;
            }

            bins.Add(new VisualizationBinModel(
                location,
                location.CoordinateX ?? 0m,
                location.CoordinateY ?? 0m,
                location.CoordinateZ ?? 0m,
                location.WidthMeters,
                location.LengthMeters,
                location.HeightMeters,
                null,
                null,
                null,
                null,
                null,
                location.LocationRole));
        }

        var visualizationRacks = racks
            .Select(rack => new VisualizationRackModel(
                rack.Id,
                rack.Type,
                rack.Origin.X,
                rack.Origin.Y,
                rack.Origin.Z,
                rack.Dimensions.Width,
                rack.Dimensions.Depth,
                rack.Dimensions.Height,
                rack.OrientationDeg,
                rack.SlotsPerLevel,
                rack.BayCount,
                rack.BackToBack,
                rack.PairedWithRackId,
                rack.GetLevels()
                    .OrderBy(x => x.Index)
                    .Select(x => new VisualizationRackLevelModel(x.Index, x.HeightFromBase))
                    .ToList()))
            .ToList();

        var slots = new List<VisualizationSlotModel>();
        foreach (var rack in racks.Where(x => RackLayoutValidator.IsSlotBased(x.Type)))
        {
            var levels = rack.GetLevels().OrderBy(x => x.Index).ToList();
            if (rack.SlotsPerLevel < 1 || levels.Count == 0)
            {
                continue;
            }

            for (var levelIndex = 0; levelIndex < levels.Count; levelIndex++)
            {
                var level = levels[levelIndex];
                var slotHeight = ResolveSlotHeight(rack, levels, levelIndex);
                if (slotHeight <= 0)
                {
                    continue;
                }

                for (var slot = 1; slot <= rack.SlotsPerLevel; slot++)
                {
                    var slotBox = ComputeSlotBounds(rack, level.HeightFromBase, slotHeight, slot, 1);
                    slots.Add(new VisualizationSlotModel(
                        BuildAddress(warehouseCode, rack.Id, level.Index, slot),
                        rack.Id,
                        level.Index,
                        slot,
                        occupiedSlots.Contains((rack.Id, level.Index, slot)),
                        slotBox.X,
                        slotBox.Y,
                        slotBox.Z,
                        slotBox.Width,
                        slotBox.Length,
                        slotBox.Height));
                }
            }
        }

        return new WarehouseGeometryResult(visualizationRacks, slots, bins);
    }

    private static bool TryCreateRackBin(
        string warehouseCode,
        Location location,
        IReadOnlyDictionary<string, RackRowDefinition> rackById,
        ISet<(string RackId, int Level, int Slot)> occupiedSlots,
        out VisualizationBinModel? bin)
    {
        bin = null;

        if (string.IsNullOrWhiteSpace(location.RackRowId) ||
            !location.ShelfLevelIndex.HasValue ||
            !location.SlotStart.HasValue)
        {
            return false;
        }

        if (!rackById.TryGetValue(location.RackRowId, out var rack) ||
            !RackLayoutValidator.IsSlotBased(rack.Type))
        {
            return false;
        }

        var levels = rack.GetLevels().OrderBy(x => x.Index).ToList();
        var levelIndex = levels.FindIndex(x => x.Index == location.ShelfLevelIndex.Value);
        if (levelIndex < 0 || rack.SlotsPerLevel < 1)
        {
            return false;
        }

        var slotSpan = Math.Max(location.SlotSpan ?? 1, 1);
        var startSlot = location.SlotStart.Value;
        if (startSlot < 1 || startSlot > rack.SlotsPerLevel || (startSlot + slotSpan - 1) > rack.SlotsPerLevel)
        {
            return false;
        }

        var slotHeight = ResolveSlotHeight(rack, levels, levelIndex);
        if (slotHeight <= 0)
        {
            return false;
        }

        var slotBounds = ComputeSlotBounds(rack, levels[levelIndex].HeightFromBase, slotHeight, startSlot, slotSpan);
        var width = Math.Max(slotBounds.Width - (Inset * 2), 0.01m);
        var length = Math.Max(slotBounds.Length - (Inset * 2), 0.01m);
        var height = Math.Max(slotBounds.Height - (Inset * 2), 0.01m);

        for (var slot = startSlot; slot < startSlot + slotSpan; slot++)
        {
            occupiedSlots.Add((rack.Id, levels[levelIndex].Index, slot));
        }

        bin = new VisualizationBinModel(
            location,
            slotBounds.X + Inset,
            slotBounds.Y + Inset,
            slotBounds.Z + Inset,
            width,
            length,
            height,
            BuildAddress(warehouseCode, rack.Id, levels[levelIndex].Index, startSlot, slotSpan),
            rack.Id,
            levels[levelIndex].Index,
            startSlot,
            slotSpan,
            location.LocationRole);

        return true;
    }

    private static decimal ResolveSlotHeight(
        RackRowDefinition rack,
        IReadOnlyList<RackLevelDefinition> levels,
        int levelIndex)
    {
        var current = levels[levelIndex].HeightFromBase;
        var upperBound = levelIndex == levels.Count - 1
            ? rack.Dimensions.Height
            : levels[levelIndex + 1].HeightFromBase;

        return upperBound - current;
    }

    private static (decimal X, decimal Y, decimal Z, decimal Width, decimal Length, decimal Height) ComputeSlotBounds(
        RackRowDefinition rack,
        decimal heightFromBase,
        decimal slotHeight,
        int startSlot,
        int slotSpan)
    {
        var slotWidth = rack.Dimensions.Width / rack.SlotsPerLevel;
        var localStartX = (startSlot - 1) * slotWidth;
        var localEndX = localStartX + (slotWidth * slotSpan);

        var points = new[]
        {
            RotatePoint(localStartX, 0m, rack),
            RotatePoint(localEndX, 0m, rack),
            RotatePoint(localStartX, rack.Dimensions.Depth, rack),
            RotatePoint(localEndX, rack.Dimensions.Depth, rack)
        };

        var minX = points.Min(x => x.X);
        var maxX = points.Max(x => x.X);
        var minY = points.Min(x => x.Y);
        var maxY = points.Max(x => x.Y);

        return (
            minX,
            minY,
            rack.Origin.Z + heightFromBase,
            maxX - minX,
            maxY - minY,
            slotHeight);
    }

    private static (decimal X, decimal Y) RotatePoint(decimal localX, decimal localY, RackRowDefinition rack)
    {
        var radians = (double)(rack.OrientationDeg * (decimal)Math.PI / 180m);
        var cos = (decimal)Math.Cos(radians);
        var sin = (decimal)Math.Sin(radians);
        return (
            rack.Origin.X + (localX * cos) - (localY * sin),
            rack.Origin.Y + (localX * sin) + (localY * cos));
    }

    private static string BuildAddress(string warehouseCode, string rackId, int levelIndex, int slotStart, int slotSpan = 1)
    {
        var baseAddress = $"{warehouseCode}-{rackId}{levelIndex}-{slotStart:D2}";
        return slotSpan > 1 ? $"{baseAddress}+{slotSpan}" : baseAddress;
    }
}
