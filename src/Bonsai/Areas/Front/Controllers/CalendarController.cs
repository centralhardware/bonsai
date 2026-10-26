using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Bonsai.Areas.Front.Logic;
using Bonsai.Areas.Front.Logic.Auth;
using Bonsai.Code.Infrastructure;
using Bonsai.Code.Services.Config;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bonsai.Areas.Front.Controllers;

/// <summary>
/// The controller for calendar-related views.
/// </summary>
[Area("front")]
[Route("util/cal")]
[Authorize(Policy = AuthRequirement.Name)]
public class CalendarController(
    CalendarPresenterService calendarSvc,
    CalendarExportPresenterService exportSvc,
    BonsaiConfigService configSvc
) : AppControllerBase
{
    /// <summary>
    /// Displays the calendar grid.
    /// </summary>
    [Route("grid")]
    public async Task<ActionResult> MonthGrid([FromQuery] int year, [FromQuery] int month)
    {
        var vm = await calendarSvc.GetMonthEventsAsync(year, month);
        return View(vm);
    }

    /// <summary>
    /// Displays the list of events for a particular day.
    /// </summary>
    [Route("list")]
    public async Task<ActionResult> DayList([FromQuery] int year, [FromQuery] int month, [FromQuery] int? day = null)
    {
        var vm = await calendarSvc.GetDayEventsAsync(year, month, day);
        return View(vm);
    }

    /// <summary>
    /// Returns all events as an iCalendar file for a logged in user.
    /// </summary>
    [Route("export.ics")]
    public async Task<ActionResult> Export()
    {
        if (!configSvc.GetDynamicConfig().CalendarExportEnabled)
            return NotFound();

        return await GetIcsFileAsync();
    }

    /// <summary>
    /// Returns all events as an iCalendar feed for an external calendar application.
    /// </summary>
    /// <remarks>
    /// External calendars poll the feed without a session, so the secret key in the address is
    /// the only means of authorization here.
    /// </remarks>
    [AllowAnonymous]
    [Route("feed/{key}.ics")]
    public async Task<ActionResult> Feed(string key)
    {
        var cfg = configSvc.GetDynamicConfig();

        if (!cfg.CalendarExportEnabled || string.IsNullOrEmpty(cfg.CalendarFeedKey))
            return NotFound();

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(key ?? ""),
                Encoding.UTF8.GetBytes(cfg.CalendarFeedKey)))
            return NotFound();

        return await GetIcsFileAsync();
    }

    #region Private helpers

    /// <summary>
    /// Renders the *.ics file for the current request.
    /// </summary>
    private async Task<ActionResult> GetIcsFileAsync()
    {
        var cfg = configSvc.GetDynamicConfig();
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var ics = await exportSvc.GetIcsFileAsync(cfg.Title, baseUrl);

        return File(Encoding.UTF8.GetBytes(ics), "text/calendar; charset=utf-8", "calendar.ics");
    }

    #endregion
}
