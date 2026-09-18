using System;
using System.Linq;
using System.Text;
using Bonsai.Code.Utils.Format;
using Xunit;

namespace Bonsai.Tests.Search;

public class IcsBuilderTests
{
    [Fact]
    public void Empty_calendar_is_well_formed()
    {
        var result = new IcsBuilder("Family").Build();

        Assert.StartsWith("BEGIN:VCALENDAR\r\n", result);
        Assert.EndsWith("END:VCALENDAR\r\n", result);
        Assert.Contains("VERSION:2.0\r\n", result);
        Assert.Contains("X-WR-CALNAME:Family\r\n", result);
    }

    [Fact]
    public void All_day_event_spans_exactly_one_day()
    {
        var builder = new IcsBuilder("Family");
        builder.AddAllDayEvent("uid@bonsai", new DateTime(2024, 12, 31), "New year");

        var result = builder.Build();

        Assert.Contains("DTSTART;VALUE=DATE:20241231\r\n", result);
        Assert.Contains("DTEND;VALUE=DATE:20250101\r\n", result);
        Assert.DoesNotContain("RRULE", result);
    }

    [Fact]
    public void Anniversary_repeats_every_year()
    {
        var builder = new IcsBuilder("Family");
        builder.AddAllDayEvent("uid@bonsai", new DateTime(1985, 2, 12), "Birthday", repeatsYearly: true);

        Assert.Contains("RRULE:FREQ=YEARLY\r\n", builder.Build());
    }

    [Fact]
    public void Anniversary_stops_at_the_specified_date()
    {
        var builder = new IcsBuilder("Family");
        builder.AddAllDayEvent("uid@bonsai", new DateTime(2010, 6, 5), "Wedding", repeatsYearly: true, repeatsUntil: new DateTime(2020, 1, 1));

        Assert.Contains("RRULE:FREQ=YEARLY;UNTIL=20200101\r\n", builder.Build());
    }

    [Fact]
    public void Special_characters_are_escaped()
    {
        var builder = new IcsBuilder("Family");
        builder.AddAllDayEvent("uid@bonsai", new DateTime(2024, 1, 1), "a\\b;c,d\ne");

        Assert.Contains("SUMMARY:a\\\\b\\;c\\,d\\ne\r\n", builder.Build());
    }

    [Fact]
    public void Long_lines_are_folded_to_75_octets()
    {
        var builder = new IcsBuilder("Family");
        builder.AddAllDayEvent("uid@bonsai", new DateTime(2024, 1, 1), new string('ё', 200));

        var lines = builder.Build().Split("\r\n");

        Assert.All(lines, x => Assert.True(Encoding.UTF8.GetByteCount(x) <= 75));
        Assert.Contains(lines, x => x.StartsWith(" "));
    }

    [Fact]
    public void Folded_line_restores_the_original_value()
    {
        var summary = new string('ё', 200);
        var builder = new IcsBuilder("Family");
        builder.AddAllDayEvent("uid@bonsai", new DateTime(2024, 1, 1), summary);

        var unfolded = builder.Build().Replace("\r\n ", "");
        var line = unfolded.Split("\r\n").First(x => x.StartsWith("SUMMARY:"));

        Assert.Equal("SUMMARY:" + summary, line);
    }
}
