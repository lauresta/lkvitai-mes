namespace LKvitai.MES.BuildingBlocks.Search;

/// <summary>
/// A screen or settings page of a module, taken from the module's own navigation definition.
/// </summary>
/// <param name="Url">Path relative to the module WebUI root, as the menu links it.</param>
/// <param name="Title">Menu label in the caller's UI language (shown).</param>
/// <param name="Subtitle">Where the screen lives, e.g. "Warehouse · Admin" (shown).</param>
/// <param name="Labels">The menu label in every UI language; the query is matched against all of them.</param>
public sealed record ScreenEntry(string Url, string Title, string? Subtitle, IReadOnlyList<string> Labels);

/// <summary>Matches a query against module screens by menu label, in any UI language, ignoring diacritics.</summary>
public static class ScreenSearch
{
    public static IReadOnlyList<SearchResultDto> Match(
        IEnumerable<ScreenEntry> screens,
        string? query,
        int limit,
        string? pathBase = null)
    {
        ArgumentNullException.ThrowIfNull(screens);

        var normalized = SearchRequest.NormalizeQuery(query);
        if (normalized is null) return [];

        return screens
            .Select((screen, index) => (screen, index, score: screen.Labels.Append(screen.Title).Max(label => SearchText.Score(label, normalized))))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ThenBy(x => x.index)
            .Select(x => (x.screen, x.score, url: SearchRequest.ToAbsolutePath(pathBase, x.screen.Url)))
            .Where(x => x.url is not null)
            .Take(SearchRequest.ClampLimit(limit))
            .Select(x => new SearchResultDto(SearchKinds.Screen, x.screen.Title, x.screen.Subtitle, x.url!, x.score))
            .ToList();
    }
}

/// <summary>Reads one resource key in several UI languages, for <see cref="ScreenEntry.Labels"/>.</summary>
public static class ScreenLabels
{
    /// <summary>
    /// The value of <paramref name="key"/> in each of <paramref name="cultures"/> (distinct, non-empty).
    /// The ambient UI culture is restored before returning.
    /// </summary>
    public static IReadOnlyList<string> InCultures(
        Microsoft.Extensions.Localization.IStringLocalizer localizer, string key, IEnumerable<string> cultures)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        ArgumentNullException.ThrowIfNull(cultures);

        var ambient = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            var labels = new List<string>();
            foreach (var culture in cultures)
            {
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(culture);
                var text = localizer[key];
                if (!text.ResourceNotFound && !string.IsNullOrWhiteSpace(text.Value) && !labels.Contains(text.Value))
                {
                    labels.Add(text.Value);
                }
            }

            return labels;
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentUICulture = ambient;
        }
    }
}
