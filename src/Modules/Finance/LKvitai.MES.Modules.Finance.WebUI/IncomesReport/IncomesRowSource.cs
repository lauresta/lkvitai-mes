namespace LKvitai.MES.Modules.Finance.WebUI.IncomesReport;

/// <summary>
/// The table behind &lt;Virtualize&gt;: rows come from the API in pages of <see cref="PageSize"/> (sorted by group first),
/// group headers and collapsed groups are laid out here, so the flat list is group header + rows of open groups.
/// </summary>
public sealed class IncomesRowSource(Func<int, int, CancellationToken, Task<IncomesRowsPageDto>> fetch)
{
    public const int PageSize = 100;
    private readonly Dictionary<int, IncomesRowDto[]> _pages = [];

    public IncomesRowsPageDto? Meta { get; private set; }
    public HashSet<string> Collapsed { get; } = new(StringComparer.Ordinal);
    public bool Grouped => Meta is { Groups.Count: > 0 };

    public async Task LoadAsync(CancellationToken ct)
    {
        var first = await fetch(0, PageSize, ct);
        _pages.Clear();
        Collapsed.Clear();
        _pages[0] = [.. first.Items];
        Meta = first;
    }

    public int FlatCount => Meta is null ? 0
        : !Grouped ? Meta.Total
        : Meta.Groups.Count + Meta.Groups.Where(g => !Collapsed.Contains(g.Key)).Sum(g => g.Count);

    public void Toggle(string groupKey)
    {
        if (!Collapsed.Remove(groupKey)) Collapsed.Add(groupKey);
    }

    /// <summary>Maps flat positions to group headers and row indices (in the server order).</summary>
    public IEnumerable<(int Flat, IncomesGroupDto? Group, int Row)> Layout(int start, int count)
    {
        if (Meta is null) yield break;
        var end = Math.Min(FlatCount, start + count);
        if (!Grouped)
        {
            for (var i = start; i < end; i++) yield return (i, null, i);
            yield break;
        }
        var flat = 0;
        var row = 0;
        foreach (var group in Meta.Groups)
        {
            if (flat >= end) yield break;
            if (flat >= start) yield return (flat, group, -1);
            flat++;
            if (!Collapsed.Contains(group.Key))
            {
                for (var i = 0; i < group.Count && flat < end; i++, flat++)
                    if (flat >= start) yield return (flat, null, row + i);
                if (flat >= end) yield break;
            }
            row += group.Count;
        }
    }

    public async Task<IReadOnlyList<IncomesFlatItem>> RangeAsync(int start, int count, CancellationToken ct)
    {
        var layout = Layout(start, count).ToList();
        await EnsureAsync(layout.Where(x => x.Group is null).Select(x => x.Row), ct);
        return layout.Select(x => x.Group is not null
            ? new IncomesFlatItem(x.Group, null, Collapsed.Contains(x.Group.Key))
            : new IncomesFlatItem(null, RowAt(x.Row), false)).ToList();
    }

    /// <summary>The first <paramref name="count"/> rows in order (phone cards).</summary>
    public async Task<IReadOnlyList<IncomesRowDto>> FirstAsync(int count, CancellationToken ct)
    {
        var total = Math.Min(count, Meta?.Total ?? 0);
        await EnsureAsync(Enumerable.Range(0, total), ct);
        return Enumerable.Range(0, total).Select(RowAt).OfType<IncomesRowDto>().ToList();
    }

    private IncomesRowDto? RowAt(int index) =>
        _pages.TryGetValue(index / PageSize, out var page) && index % PageSize < page.Length ? page[index % PageSize] : null;

    private async Task EnsureAsync(IEnumerable<int> rows, CancellationToken ct)
    {
        foreach (var page in rows.Select(row => row / PageSize).Distinct().Where(page => !_pages.ContainsKey(page)).ToList())
        {
            var result = await fetch(page * PageSize, PageSize, ct);
            _pages[page] = [.. result.Items];
        }
    }
}
