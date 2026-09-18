using System;
using System.Linq;
using Bonsai.Areas.Front.ViewModels.Calendar;
using Bonsai.Code.Utils.Format;
using Xunit;

namespace Bonsai.Tests.Search;

public class IcsSerializerTests
{
    [Fact]
    public void Empty_calendar_is_well_formed()
    {
        var result = IcsSerializer.Serialize("Family", []);

        Assert.StartsWith("BEGIN:VCALENDAR", result);
        Assert.Contains("VERSION:2.0", result);
        Assert.Contains("X-WR-CALNAME:Family", result);
        Assert.EndsWith("END:VCALENDAR\r\n", result);
    }

    [Fact]
    public void All_day_event_spans_exactly_one_day()
    {
        var result = Serialize(new CalendarExportEventVM
        {
            Uid = "uid@bonsai",
            Date = new DateTime(2024, 12, 31),
            Title = "New year"
        });

        Assert.Contains("DTSTART;VALUE=DATE:20241231", result);
        Assert.Contains("DTEND;VALUE=DATE:20250101", result);
        Assert.DoesNotContain("RRULE", result);
    }

    [Fact]
    public void Anniversary_repeats_every_year()
    {
        var result = Serialize(new CalendarExportEventVM
        {
            Uid = "uid@bonsai",
            Date = new DateTime(1985, 2, 12),
            Title = "Birthday",
            RepeatsYearly = true
        });

        Assert.Contains("RRULE:FREQ=YEARLY", result);
        Assert.DoesNotContain("UNTIL", result);
    }

    [Fact]
    public void Anniversary_stops_at_the_specified_date()
    {
        var result = Serialize(new CalendarExportEventVM
        {
            Uid = "uid@bonsai",
            Date = new DateTime(2010, 6, 5),
            Title = "Wedding",
            RepeatsYearly = true,
            RepeatsUntil = new DateTime(2020, 1, 1)
        });

        Assert.Contains("UNTIL=20200101", result);
    }

    [Fact]
    public void Special_characters_are_escaped()
    {
        var result = Serialize(new CalendarExportEventVM
        {
            Uid = "uid@bonsai",
            Date = new DateTime(2024, 1, 1),
            Title = "Wedding; Ivan, Maria\nand guests"
        });

        Assert.Contains("SUMMARY:Wedding\\; Ivan\\, Maria\\nand guests", Unfold(result));
    }

    [Fact]
    public void Long_title_is_folded_and_restored()
    {
        var title = new string('ё', 200);
        var result = Serialize(new CalendarExportEventVM
        {
            Uid = "uid@bonsai",
            Date = new DateTime(2024, 1, 1),
            Title = title
        });

        Assert.Contains(result.Split("\r\n"), x => x.StartsWith(" "));
        Assert.Contains(Unfold(result).Split("\r\n"), x => x == "SUMMARY:" + title);
    }

    #region Private helpers

    private static string Serialize(CalendarExportEventVM evt) => IcsSerializer.Serialize("Family", [evt]);

    private static string Unfold(string ics) => ics.Replace("\r\n ", "");

    #endregion
}
