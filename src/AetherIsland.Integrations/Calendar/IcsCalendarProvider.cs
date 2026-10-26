using System.Net.Http;
using System.Text;

namespace AetherIsland.Integrations.Calendar;

public sealed class CalendarEvent
{
    public string Title { get; set; } = "";
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
    public bool AllDay { get; set; }
    public string? Location { get; set; }
    public string? Url { get; set; }
    public string? Description { get; set; }
    public string Source { get; set; } = "";
}

/// <summary>
/// Calendar provider that reads standard iCalendar (.ics) sources: public URLs and local files.
/// Official OAuth connectors for Google/Outlook are architecturally planned but not enabled —
/// this module works with ICS exports, which both services offer.
/// </summary>
public sealed class IcsCalendarProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(12) };

    public async Task<List<CalendarEvent>> LoadAsync(IEnumerable<string> sources, CancellationToken ct = default)
    {
        var events = new List<CalendarEvent>();
        foreach (var source in sources.Where(s => !string.IsNullOrWhiteSpace(s)))
        {
            try
            {
                var ics = await FetchAsync(source.Trim(), ct);
                if (ics is null) continue;
                var parsed = IcsParser.Parse(ics, source.Trim());
                events.AddRange(parsed);
            }
            catch
            {
                // a broken source must not take down the whole calendar
            }
        }
        return events.OrderBy(e => e.Start).ToList();
    }

    private static async Task<string?> FetchAsync(string source, CancellationToken ct)
    {
        if (File.Exists(source))
            return await File.ReadAllTextAsync(source, ct);
        if (source.StartsWith("http://") || source.StartsWith("https://"))
            return await Http.GetStringAsync(source, ct);
        return null;
    }
}

public static class IcsParser
{
    public static List<CalendarEvent> Parse(string icsText, string sourceName)
    {
        var lines = Unfold(icsText);
        var events = new List<CalendarEvent>();

        // collect VEVENT blocks
        string? current = null;
        var block = new List<string>();
        foreach (var line in lines)
        {
            if (line.StartsWith("BEGIN:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                current = "vevent";
                block.Clear();
                continue;
            }
            if (line.StartsWith("END:VEVENT", StringComparison.OrdinalIgnoreCase))
            {
                if (current == "vevent") events.AddRange(ParseEvent(block, sourceName));
                current = null;
                continue;
            }
            if (current == "vevent") block.Add(line);
        }
        return events;
    }

    private static List<CalendarEvent> ParseEvent(List<string> block, string sourceName)
    {
        var summary = GetProp(block, "SUMMARY");
        if (summary is null) return new List<CalendarEvent>(); // must have a title

        var dtStart = GetProp(block, "DTSTART");
        if (dtStart is null) return new List<CalendarEvent>();
        var start = ParseIcsDate(dtStart.Value.Val, out var allDay);
        var end = GetProp(block, "DTEND") is { } e ? ParseIcsDate(e.Val, out _) : start.AddHours(1);
        if (end <= start && !allDay) end = start.AddHours(1);
        if (allDay) end = start.AddDays(1);

        var url = GetProp(block, "URL")?.Val;
        var location = GetProp(block, "LOCATION")?.Val;
        var desc = GetProp(block, "DESCRIPTION")?.Val;

        var ev = new CalendarEvent
        {
            Title = summary.Value.Val,
            Start = start,
            End = end,
            AllDay = allDay,
            Location = location,
            Url = ExtractMeetingUrl(url, desc, location),
            Description = desc,
            Source = sourceName
        };

        var list = new List<CalendarEvent> { ev };

        // basic RRULE expansion (DAILY/WEEKLY/MONTHLY + INTERVAL + COUNT/UNTIL)
        var rrule = GetProp(block, "RRULE");
        if (rrule is not null)
        {
            list.AddRange(Expand(ev, rrule.Value.Val));
        }
        return list;
    }

    private static IEnumerable<CalendarEvent> Expand(CalendarEvent template, string rrule)
    {
        var parts = rrule.Split(';').Select(p => p.Split('=', 2)).Where(p => p.Length == 2)
            .ToDictionary(p => p[0].ToUpperInvariant(), p => p[1], StringComparer.OrdinalIgnoreCase);
        if (!parts.TryGetValue("FREQ", out var freq)) yield break;
        freq = freq.ToUpperInvariant();
        var interval = parts.TryGetValue("INTERVAL", out var iv) && int.TryParse(iv, out var i) ? Math.Max(1, i) : 1;
        var count = parts.TryGetValue("COUNT", out var c) && int.TryParse(c, out var cc) ? Math.Min(cc, 200) : 60;
        if (parts.TryGetValue("UNTIL", out var untilStr))
        {
            var until = ParseIcsDate(untilStr, out _);
            count = (int)Math.Min(count, (until - template.Start).TotalDays * 24 + 2);
        }
        if (freq is not "DAILY" and not "WEEKLY" and not "MONTHLY") yield break; // BYDAY etc. unsupported

        var start = template.Start;
        var end = template.End;
        for (var n = 1; n < count; n++)
        {
            var next = freq switch
            {
                "DAILY" => start.AddDays(interval * n),
                "WEEKLY" => start.AddDays(7 * interval * n),
                _ => start.AddMonths(interval * n)
            };
            var duration = end - start;
            yield return new CalendarEvent
            {
                Title = template.Title,
                Start = next,
                End = next + duration,
                AllDay = template.AllDay,
                Location = template.Location,
                Url = template.Url,
                Description = template.Description,
                Source = template.Source
            };
            if (next > DateTime.Now.AddYears(1)) yield break;
        }
    }

    private static (string Name, string Val)? GetProp(List<string> block, string name)
    {
        foreach (var line in block)
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            var left = line[..idx];
            if (left.Split(';')[0].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return (left, line[(idx + 1)..]);
            }
        }
        return null;
    }

    private static DateTime ParseIcsDate(string raw, out bool allDay)
    {
        allDay = raw.Length == 8 && raw.All(char.IsDigit);
        if (allDay) return DateTime.ParseExact(raw, "yyyyMMdd", null);
        if (raw.EndsWith("Z"))
        {
            var utc = DateTime.ParseExact(raw.TrimEnd('Z'), "yyyyMMdd'T'HHmmss", null, System.Globalization.DateTimeStyles.AssumeUniversal);
            return utc.ToLocalTime();
        }
        var dateOnly = raw.Split('T')[0];
        if (raw.Contains('T'))
            return DateTime.ParseExact(raw, "yyyyMMdd'T'HHmmss", null);
        return DateTime.Parse(dateOnly);
    }

    /// <summary>Detects Teams/Zoom/Meet links from URL/DESCRIPTION/LOCATION fields.</summary>
    public static string? ExtractMeetingUrl(string? url, string? desc, string? location)
    {
        foreach (var candidate in new[] { url, desc, location })
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            foreach (var token in candidate.Split(new[] { ' ', '\n', '\r', '<', '>' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (token.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    token.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    var lower = token.ToLowerInvariant();
                    if (lower.Contains("teams.microsoft") || lower.Contains("zoom.us") ||
                        lower.Contains("meet.google") || lower.Contains("webex"))
                        return token;
                }
            }
        }
        return url;
    }

    private static List<string> Unfold(string text)
    {
        var raw = text.Replace("\r\n", "\n").Split('\n');
        var lines = new List<string>();
        foreach (var line in raw)
        {
            if (line.Length == 0) continue;
            if ((line[0] == ' ' || line[0] == '\t') && lines.Count > 0)
                lines[^1] += line[1..];
            else
                lines.Add(line);
        }
        return lines;
    }
}
