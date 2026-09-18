using System;
using System.Text;

namespace Bonsai.Code.Utils.Format;

/// <summary>
/// Serializer for the iCalendar format (RFC 5545).
/// </summary>
public class IcsBuilder
{
    public IcsBuilder(string calendarName)
    {
        _sb = new StringBuilder();

        AppendLine("BEGIN:VCALENDAR");
        AppendLine("VERSION:2.0");
        AppendLine("PRODID:-//Bonsai//Bonsai Family Wiki//EN");
        AppendLine("CALSCALE:GREGORIAN");
        AppendLine("METHOD:PUBLISH");
        AppendLine("NAME:" + Escape(calendarName));
        AppendLine("X-WR-CALNAME:" + Escape(calendarName));
        AppendLine("REFRESH-INTERVAL;VALUE=DURATION:PT12H");
        AppendLine("X-PUBLISHED-TTL:PT12H");
    }

    private readonly StringBuilder _sb;

    /// <summary>
    /// Maximum length of a content line in octets (RFC 5545, 3.1), excluding the line break.
    /// </summary>
    private const int MaxLineLength = 75;

    /// <summary>
    /// Adds an all-day event, optionally repeating every year.
    /// </summary>
    /// <param name="uid">Globally unique and stable identifier of the event.</param>
    /// <param name="date">Date of the event (or of its first occurrence).</param>
    /// <param name="summary">Title of the event.</param>
    /// <param name="url">Link to the related page (optional).</param>
    /// <param name="repeatsYearly">Flag indicating that the event is an anniversary.</param>
    /// <param name="repeatsUntil">Date of the last occurrence for a yearly event (optional).</param>
    public void AddAllDayEvent(string uid, DateTime date, string summary, string url = null, bool repeatsYearly = false, DateTime? repeatsUntil = null)
    {
        AppendLine("BEGIN:VEVENT");
        AppendLine("UID:" + Escape(uid));
        AppendLine("DTSTAMP:" + FormatTimestamp(DateTime.UtcNow));
        AppendLine("DTSTART;VALUE=DATE:" + FormatDate(date));
        AppendLine("DTEND;VALUE=DATE:" + FormatDate(date.AddDays(1)));
        AppendLine("SUMMARY:" + Escape(summary));

        if (!string.IsNullOrEmpty(url))
            AppendLine("URL;VALUE=URI:" + Escape(url));

        if (repeatsYearly)
        {
            var rule = "RRULE:FREQ=YEARLY";
            if (repeatsUntil is DateTime until)
                rule += ";UNTIL=" + FormatDate(until);

            AppendLine(rule);
        }

        AppendLine("TRANSP:TRANSPARENT");
        AppendLine("END:VEVENT");
    }

    /// <summary>
    /// Returns the contents of the calendar file.
    /// </summary>
    public string Build()
    {
        return _sb + "END:VCALENDAR\r\n";
    }

    #region Private helpers

    /// <summary>
    /// Appends a content line, folding it if it is too long.
    /// </summary>
    private void AppendLine(string line)
    {
        var bytes = Encoding.UTF8.GetByteCount(line);
        if (bytes <= MaxLineLength)
        {
            _sb.Append(line).Append("\r\n");
            return;
        }

        // A folded line is continued by a line starting with a space, so a continuation
        // can only carry MaxLineLength - 1 octets of payload.
        var used = 0;
        var limit = MaxLineLength;
        for (var i = 0; i < line.Length; i++)
        {
            // surrogate pairs must not be split across a fold
            var len = char.IsHighSurrogate(line[i]) && i + 1 < line.Length
                ? Encoding.UTF8.GetByteCount(line.Substring(i, 2))
                : Encoding.UTF8.GetByteCount(line[i].ToString());

            if (used + len > limit)
            {
                _sb.Append("\r\n ");
                used = 1;
                limit = MaxLineLength;
            }

            _sb.Append(line[i]);
            if (len > 1 && char.IsHighSurrogate(line[i]) && i + 1 < line.Length)
            {
                _sb.Append(line[i + 1]);
                i++;
            }

            used += len;
        }

        _sb.Append("\r\n");
    }

    /// <summary>
    /// Escapes the special characters in a text value (RFC 5545, 3.3.11).
    /// </summary>
    private static string Escape(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        return value.Replace("\\", "\\\\")
                    .Replace(";", "\\;")
                    .Replace(",", "\\,")
                    .Replace("\r\n", "\\n")
                    .Replace("\n", "\\n")
                    .Replace("\r", "\\n");
    }

    /// <summary>
    /// Formats a date-only value.
    /// </summary>
    private static string FormatDate(DateTime date) => date.ToString("yyyyMMdd");

    /// <summary>
    /// Formats an UTC timestamp.
    /// </summary>
    private static string FormatTimestamp(DateTime date) => date.ToString("yyyyMMdd'T'HHmmss'Z'");

    #endregion
}
