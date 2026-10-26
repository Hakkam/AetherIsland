using AetherIsland.Integrations.Calendar;
using Xunit;

namespace AetherIsland.Tests;

public class IcsParserTests
{
    private const string SimpleIcs = """
        BEGIN:VCALENDAR
        VERSION:2.0
        BEGIN:VEVENT
        SUMMARY:Team standup
        DTSTART:20261026T090000Z
        DTEND:20261026T091500Z
        LOCATION:Teams
        URL:https://teams.microsoft.com/l/meetup-join/abc
        END:VEVENT
        END:VCALENDAR
        """;

    [Fact]
    public void ParsesBasicEvent_WithUtcTimes()
    {
        var events = IcsParser.Parse(SimpleIcs, "test");
        var ev = Assert.Single(events);
        Assert.Equal("Team standup", ev.Title);
        Assert.Equal(DateTimeKind.Local, ev.Start.Kind);
        Assert.Equal(TimeSpan.FromMinutes(15), ev.End - ev.Start);
        Assert.False(ev.AllDay);
    }

    [Fact]
    public void ExtractsTeamsMeetingUrl()
    {
        var events = IcsParser.Parse(SimpleIcs, "test");
        Assert.Equal("https://teams.microsoft.com/l/meetup-join/abc", Assert.Single(events).Url);
    }

    [Fact]
    public void ParsesAllDayEvent()
    {
        const string ics = """
            BEGIN:VEVENT
            SUMMARY:Off day
            DTSTART;VALUE=DATE:20261105
            DTEND;VALUE=DATE:20261106
            END:VEVENT
            """;
        var ev = Assert.Single(IcsParser.Parse(ics, "test"));
        Assert.True(ev.AllDay);
        Assert.Equal(new DateTime(2026, 11, 5), ev.Start.Date);
    }

    [Fact]
    public void UnfoldsContinuationLines()
    {
        const string ics = """
            BEGIN:VEVENT
            SUMMARY:Long title that
              continues here
            DTSTART:20261026T100000Z
            END:VEVENT
            """;
        var ev = Assert.Single(IcsParser.Parse(ics, "test"));
        // single leading space per RFC 5545 unfolding
        Assert.Equal("Long title that continues here", ev.Title);
    }

    [Fact]
    public void ExpandsWeeklyRecurrence()
    {
        const string ics = """
            BEGIN:VEVENT
            SUMMARY:Weekly sync
            DTSTART:20261026T100000
            DTEND:20261026T110000
            RRULE:FREQ=WEEKLY;COUNT=4
            END:VEVENT
            """;
        var events = IcsParser.Parse(ics, "test");
        Assert.Equal(4, events.Count);
        Assert.All(events, e => Assert.Equal("Weekly sync", e.Title));
        // 4th occurrence is 3 weeks after the first
        Assert.Equal(events[0].Start.AddDays(21), events[3].Start);
    }

    [Fact]
    public void SkipsEventsWithoutSummary()
    {
        const string ics = """
            BEGIN:VEVENT
            DTSTART:20261026T100000Z
            END:VEVENT
            """;
        Assert.Empty(IcsParser.Parse(ics, "test"));
    }

    [Fact]
    public void DetectsZoomLinkInDescription()
    {
        const string ics = """
            BEGIN:VEVENT
            SUMMARY:Design review
            DTSTART:20261026T140000Z
            DESCRIPTION:Join us https://zoom.us/j/123456 thanks
            END:VEVENT
            """;
        var ev = Assert.Single(IcsParser.Parse(ics, "test"));
        Assert.Equal("https://zoom.us/j/123456", ev.Url);
    }
}
