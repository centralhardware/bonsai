using System;

namespace Bonsai.Areas.Front.ViewModels.Calendar;

/// <summary>
/// An event prepared for exporting to an external calendar.
/// </summary>
public class CalendarExportEventVM
{
    /// <summary>
    /// Globally unique and stable identifier of the event.
    /// </summary>
    public string Uid { get; init; }

    /// <summary>
    /// Date of the event (or of its first occurrence).
    /// </summary>
    public DateTime Date { get; init; }

    /// <summary>
    /// Title of the event.
    /// </summary>
    public string Title { get; init; }

    /// <summary>
    /// Link to the related page (optional).
    /// </summary>
    public string Url { get; init; }

    /// <summary>
    /// Flag indicating that the event is an anniversary and repeats every year.
    /// </summary>
    public bool RepeatsYearly { get; init; }

    /// <summary>
    /// Date of the last occurrence of a yearly event (optional).
    /// </summary>
    public DateTime? RepeatsUntil { get; init; }
}
