namespace LKvitai.MES.BuildingBlocks.Search;

/// <summary>
/// A screen or settings page of a module, taken from the module's own navigation definition.
/// </summary>
/// <param name="Url">Path relative to the module WebUI root, as the menu links it.</param>
/// <param name="Title">Menu label in the caller's UI language (shown).</param>
/// <param name="Subtitle">Where the screen lives, e.g. "Warehouse · Admin" (shown).</param>
/// <param name="Labels">The menu label in every UI language; the query is matched against all of them.</param>
/// <param name="Rooted">
/// True when <paramref name="Url"/> is already an absolute path on the portal host (a Portal module
/// tile such as <c>/warehouse/</c>) and must not be prefixed with the module's path base.
/// </param>
public sealed record ScreenEntry(string Url, string Title, string? Subtitle, IReadOnlyList<string> Labels, bool Rooted = false)
{
    /// <summary>
    /// A screen whose label is one resource key: shown in the current UI language, matched in all of
    /// <paramref name="cultures"/>.
    /// </summary>
    public static ScreenEntry FromResource(
        Microsoft.Extensions.Localization.IStringLocalizer text, string url, string labelKey, string? subtitle, IEnumerable<string> cultures)
    {
        ArgumentNullException.ThrowIfNull(text);
        return new ScreenEntry(url, text[labelKey].Value, subtitle, ScreenLabels.InCultures(text, labelKey, cultures));
    }
}

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
            .Select(x => (x.screen, x.score, url: SearchRequest.ToAbsolutePath(x.screen.Rooted ? null : pathBase, x.screen.Url)))
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

        return InCultures(
            () =>
            {
                var text = localizer[key];
                return text.ResourceNotFound ? null : text.Value;
            },
            cultures);
    }

    /// <summary>
    /// What <paramref name="read"/> returns under each of <paramref name="cultures"/> (distinct, non-empty).
    /// The ambient UI culture is restored before returning.
    /// </summary>
    public static IReadOnlyList<string> InCultures(Func<string?> read, IEnumerable<string> cultures)
    {
        ArgumentNullException.ThrowIfNull(read);
        ArgumentNullException.ThrowIfNull(cultures);

        var ambient = System.Globalization.CultureInfo.CurrentUICulture;
        try
        {
            var labels = new List<string>();
            foreach (var culture in cultures)
            {
                System.Globalization.CultureInfo.CurrentUICulture = new System.Globalization.CultureInfo(culture);
                var text = read();
                if (!string.IsNullOrWhiteSpace(text) && !labels.Contains(text))
                {
                    labels.Add(text);
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
