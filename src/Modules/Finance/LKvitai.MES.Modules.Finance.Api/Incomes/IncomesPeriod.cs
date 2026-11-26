using System.Globalization;

namespace LKvitai.MES.Modules.Finance.Api.Incomes;

/// <summary>Period arithmetic of the report. All periods are inclusive of both the first and the last day.</summary>
public static class IncomesPeriod
{
    /// <summary>Longest period the report accepts (about three years).</summary>
    public const int MaxDays = 1100;
    /// <summary>Above this the chart and the default grouping switch from days to months.</summary>
    public const int LongPeriodDays = 62;
    /// <summary>Daily chart buckets are offered up to a year.</summary>
    public const int MaxDailyBucketDays = 370;

    public static (DateOnly From, DateOnly To) LastMonth(DateOnly today)
    {
        var first = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        return (first, LastDayOfMonth(first));
    }

    /// <summary>
    /// SQL bounds for an inclusive period: <c>date &gt;= from AND date &lt; to + 1 day</c>. The legacy window used
    /// strict comparisons and lost the first and the last day of the period.
    /// </summary>
    public static (DateTime FromInclusive, DateTime ToExclusive) SqlBounds(DateOnly from, DateOnly to) =>
        (from.ToDateTime(TimeOnly.MinValue), to.AddDays(1).ToDateTime(TimeOnly.MinValue));

    public static DateOnly LastDayOfMonth(DateOnly date) =>
        new(date.Year, date.Month, DateTime.DaysInMonth(date.Year, date.Month));

    public static bool IsLastDayOfMonth(DateOnly date) => date.Day == DateTime.DaysInMonth(date.Year, date.Month);

    public static int Workdays(DateOnly from, DateOnly to)
    {
        var count = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
            if (!IsWeekend(day)) count++;
        return count;
    }

    public static bool IsWeekend(DateOnly day) => day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    /// <summary>
    /// The previous period of the same length: a month (or month to date) → the previous month, a quarter →
    /// the previous quarter, a year to date → the previous year; any other range → the same number of days
    /// right before it.
    /// </summary>
    public static (DateOnly From, DateOnly To) Previous(DateOnly from, DateOnly to)
    {
        if (from.Day == 1)
        {
            var span = MonthIndex(to) - MonthIndex(from) + 1;
            var months = span;
            if (from.Month == 1 && to.Year == from.Year && span > 3) months = 12;
            else if ((from.Month - 1) % 3 == 0 && span is > 1 and <= 3) months = 3;
            return Shift(from, to, months);
        }
        var days = to.DayNumber - from.DayNumber + 1;
        var previousTo = from.AddDays(-1);
        return (previousTo.AddDays(-(days - 1)), previousTo);
    }

    /// <summary>The same period one year earlier (a whole month stays a whole month, e.g. February).</summary>
    public static (DateOnly From, DateOnly To) LastYear(DateOnly from, DateOnly to) => Shift(from, to, 12);

    private static (DateOnly From, DateOnly To) Shift(DateOnly from, DateOnly to, int months)
    {
        var shiftedTo = to.AddMonths(-months);
        if (IsLastDayOfMonth(to)) shiftedTo = LastDayOfMonth(shiftedTo);
        return (from.AddMonths(-months), shiftedTo);
    }

    private static int MonthIndex(DateOnly date) => date.Year * 12 + date.Month - 1;

    /// <summary>Default chart bucket: days up to <see cref="LongPeriodDays"/>, months above.</summary>
    public static string DefaultBucket(DateOnly from, DateOnly to) =>
        to.DayNumber - from.DayNumber + 1 > LongPeriodDays ? "month" : "day";

    public static string BucketKey(DateOnly date, string bucket) => bucket switch
    {
        "week" => WeekKey(date),
        "month" => MonthKey(date),
        _ => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
    };

    public static string WeekKey(DateOnly date)
    {
        var at = date.ToDateTime(TimeOnly.MinValue);
        return string.Create(CultureInfo.InvariantCulture, $"{ISOWeek.GetYear(at)}-W{ISOWeek.GetWeekOfYear(at):00}");
    }

    public static int Week(DateOnly date) => ISOWeek.GetWeekOfYear(date.ToDateTime(TimeOnly.MinValue));

    public static string MonthKey(DateOnly date) => date.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>Buckets of the period in order, each clipped to the period.</summary>
    public static IReadOnlyList<(string Key, DateOnly From, DateOnly To)> Buckets(DateOnly from, DateOnly to, string bucket)
    {
        var result = new List<(string Key, DateOnly From, DateOnly To)>();
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var key = BucketKey(day, bucket);
            if (result.Count > 0 && result[^1].Key == key) result[^1] = (key, result[^1].From, day);
            else result.Add((key, day, day));
        }
        return result;
    }
}
