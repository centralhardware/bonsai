using System;
using System.Collections.Generic;
using Bonsai.Areas.Front.ViewModels.Calendar;
using Ical.Net;
using Ical.Net.CalendarComponents;
using Ical.Net.DataTypes;
using Ical.Net.Serialization;

namespace Bonsai.Code.Utils.Format;

/// <summary>
/// Renders the events as an iCalendar file (RFC 5545).
/// </summary>
public static class IcsSerializer
{
    /// <summary>
    /// Product identifier of the generated files.
    /// </summary>
    private const string ProductId = "-//Bonsai//Bonsai Family Wiki//EN";

    /// <summary>
    /// How often an external calendar is advised to refresh the feed.
    /// </summary>
    private const string RefreshInterval = "PT12H";

    /// <summary>
    /// Renders the calendar's contents.
    /// </summary>
    /// <param name="calendarName">Title of the calendar (usually the title of the website).</param>
    /// <param name="events">Events to include.</param>
    public static string Serialize(string calendarName, IEnumerable<CalendarExportEventVM> events)
    {
        var calendar = new Calendar { ProductId = ProductId, Method = "PUBLISH" };

        calendar.AddProperty("NAME", calendarName);
        calendar.AddProperty("X-WR-CALNAME", calendarName);
        calendar.AddProperty("REFRESH-INTERVAL;VALUE=DURATION", RefreshInterval);
        calendar.AddProperty("X-PUBLISHED-TTL", RefreshInterval);

        foreach (var evt in events)
            calendar.Events.Add(Map(evt));

        return new CalendarSerializer().SerializeToString(calendar);
    }

    #region Private helpers

    /// <summary>
    /// Converts an event to the library's representation.
    /// </summary>
    private static CalendarEvent Map(CalendarExportEventVM evt)
    {
        var result = new CalendarEvent
        {
            Uid = evt.Uid,
            Summary = evt.Title,
            // a date without a time component makes the event last the entire day
            DtStart = new CalDateTime(DateOnly.FromDateTime(evt.Date)),
            DtEnd = new CalDateTime(DateOnly.FromDateTime(evt.Date.AddDays(1))),
            Transparency = TransparencyType.Transparent
        };

        if (!string.IsNullOrEmpty(evt.Url))
            result.Url = new Uri(evt.Url);

        if (evt.RepeatsYearly)
        {
            result.RecurrenceRules.Add(new RecurrencePattern(FrequencyType.Yearly, 1)
            {
                Until = evt.RepeatsUntil is DateTime until
                    ? new CalDateTime(DateOnly.FromDateTime(until))
                    : null
            });
        }

        return result;
    }

    #endregion
}
