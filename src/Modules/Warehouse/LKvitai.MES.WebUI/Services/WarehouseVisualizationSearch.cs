using LKvitai.MES.Modules.Warehouse.WebUI.Models;

namespace LKvitai.MES.Modules.Warehouse.WebUI.Services;

public static class WarehouseVisualizationSearch
{
    public static List<string> GetSuggestions(
        IReadOnlyList<VisualizationBinDto> bins,
        string query,
        int maxResults = 8)
    {
        if (bins.Count == 0 || string.IsNullOrWhiteSpace(query) || maxResults <= 0)
        {
            return [];
        }

        var trimmed = query.Trim();

        return bins
            .Select(x => x.Code)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(code => new
            {
                Code = code,
                Score = ComputeScore(code, trimmed)
            })
            .Where(x => x.Score >= 0)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(x => x.Code)
            .ToList();
    }

    public static VisualizationBinDto? FindBestMatch(IReadOnlyList<VisualizationBinDto> bins, string query)
    {
        if (bins.Count == 0 || string.IsNullOrWhiteSpace(query))
        {
            return null;
        }

        var normalized = query.Trim();
        return bins
            .Select(bin => new
            {
                Bin = bin,
                Score = ComputeScore(bin.Code, normalized)
            })
            .Where(x => x.Score >= 0)
            .OrderBy(x => x.Score)
            .ThenBy(x => x.Bin.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Bin)
            .FirstOrDefault();
    }

    private static int ComputeScore(string code, string query)
    {
        if (string.Equals(code, query, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (code.StartsWith(query, StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return code.Contains(query, StringComparison.OrdinalIgnoreCase) ? 2 : -1;
    }
}
