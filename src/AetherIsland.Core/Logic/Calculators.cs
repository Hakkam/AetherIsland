namespace AetherIsland.Core.Logic;

public static class DayProgressCalculator
{
    public static TimeSpan ParseTime(string hhmm, TimeSpan fallback)
    {
        if (TimeSpan.TryParse(hhmm, out var t)) return t;
        return fallback;
    }

    /// <summary>Progress through the wake→bed window, clamped 0..1.</summary>
    public static double DayFraction(DateTime now, TimeSpan wake, TimeSpan bed)
    {
        var t = now.TimeOfDay;
        if (bed <= wake) bed += TimeSpan.FromHours(24); // past-midnight bedtime
        if (t <= wake) return 0;
        if (t >= bed) return 1;
        return (t - wake) / (bed - wake);
    }

    /// <summary>Progress through the work window. Returns null outside the workday.</summary>
    public static double? WorkFraction(DateTime now, TimeSpan start, TimeSpan end)
    {
        if (end <= start) return null;
        var t = now.TimeOfDay;
        if (t < start || t >= end) return null;
        return (t - start) / (end - start);
    }

    public static TimeSpan RemainingUntil(DateTime now, string bedTime)
    {
        var bed = ParseTime(bedTime, new TimeSpan(23, 30, 0));
        var target = now.Date + bed;
        if (target <= now) target = target.AddDays(1);
        return target - now;
    }
}

public static class QuotaMath
{
    /// <summary>
    /// Percent used of a quota. Returns null when the limit is unknown/zero —
    /// callers must show "Not available from provider" instead of a fabricated percentage.
    /// </summary>
    public static double? PercentUsed(double? used, double? limit)
    {
        if (used is null || limit is null || limit <= 0) return null;
        return Math.Clamp(used.Value / limit.Value * 100.0, 0, 999);
    }

    public static double? Remaining(double? limit, double? used)
    {
        if (limit is null) return null;
        var u = used ?? 0;
        return Math.Max(0, limit.Value - u);
    }

    /// <summary>Highest configured alert threshold crossed by percent (null-safe).</summary>
    public static int? CrossedThreshold(double? percent, IEnumerable<int> thresholds)
    {
        if (percent is null) return null;
        int? best = null;
        foreach (var t in thresholds)
            if (percent >= t && (best is null || t > best)) best = t;
        return best;
    }

    public static string FormatTokens(long? tokens)
    {
        if (tokens is null) return "—";
        return tokens switch
        {
            >= 1_000_000_000 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{tokens / 1_000_000_000.0:0.##}B"),
            >= 1_000_000 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{tokens / 1_000_000.0:0.##}M"),
            >= 1_000 => string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{tokens / 1_000.0:0.#}K"),
            _ => tokens.Value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)
        };
    }
}
