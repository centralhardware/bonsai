using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bonsai.Areas.Front.ViewModels.Calendar;
using Bonsai.Code.DomainModel.Relations;
using Bonsai.Code.Utils.Date;
using Bonsai.Code.Utils.Format;
using Bonsai.Data;
using Bonsai.Data.Models;
using Bonsai.Localization;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;

namespace Bonsai.Areas.Front.Logic;

/// <summary>
/// The service that exports the calendar events in the iCalendar format (RFC 5545).
/// </summary>
public class CalendarExportPresenterService
{
    public CalendarExportPresenterService(AppDbContext db)
    {
        _db = db;
    }

    private readonly AppDbContext _db;

    /// <summary>
    /// Returns the contents of the *.ics file with all known events.
    /// </summary>
    /// <param name="calendarName">Title of the calendar (usually the title of the website).</param>
    /// <param name="baseUrl">Absolute URL of the website, used for links to pages.</param>
    public async Task<string> GetIcsFileAsync(string calendarName, string baseUrl)
    {
        var context = await RelationContext.LoadContextAsync(_db);

        var events = GetPageEvents(context, baseUrl)
                     .Concat(GetRelationEvents(context, baseUrl))
                     .Concat(await GetOneTimeEventsAsync(baseUrl));

        return IcsSerializer.Serialize(calendarName, events);
    }

    #region Private helpers

    /// <summary>
    /// Infers birth and death events for all pages.
    /// </summary>
    private IEnumerable<CalendarExportEventVM> GetPageEvents(RelationContext context, string baseUrl)
    {
        foreach (var page in context.Pages.Values)
        {
            // a birthday of a deceased person is not an ongoing anniversary
            if (page.DeathDate == null && page.BirthDate is FuzzyDate birth && GetAnniversaryDate(birth) is DateTime birthDate)
            {
                yield return new CalendarExportEventVM
                {
                    Uid = $"birth-{page.Id:N}@bonsai",
                    Date = birthDate,
                    Title = FormatTitle(page.Title, Texts.CalendarPresenter_Birthday_Anniversary),
                    Url = GetPageUrl(baseUrl, page.Key),
                    RepeatsYearly = true
                };
            }

            if (page.DeathDate is FuzzyDate death && GetAnniversaryDate(death) is DateTime deathDate)
            {
                yield return new CalendarExportEventVM
                {
                    Uid = $"death-{page.Id:N}@bonsai",
                    Date = deathDate,
                    Title = FormatTitle(page.Title, Texts.CalendarPresenter_Death_Anniversary),
                    Url = GetPageUrl(baseUrl, page.Key),
                    RepeatsYearly = true
                };
            }
        }
    }

    /// <summary>
    /// Infers relation-based events (weddings, adoptions).
    /// </summary>
    private IEnumerable<CalendarExportEventVM> GetRelationEvents(RelationContext context, string baseUrl)
    {
        var visited = new HashSet<string>();

        foreach (var rel in context.Relations.SelectMany(x => x.Value))
        {
            if (rel.Duration is not FuzzyRange duration || duration.RangeStart is not FuzzyDate start)
                continue;

            if (GetAnniversaryDate(start) is not DateTime date)
                continue;

            var hash = string.Concat(rel.SourceId.ToString(), rel.DestinationId.ToString(), duration.ToString());
            if (!visited.Add(hash))
                continue;

            visited.Add(string.Concat(rel.DestinationId.ToString(), rel.SourceId.ToString(), duration.ToString()));

            var source = context.Pages[rel.SourceId];
            var dest = context.Pages[rel.DestinationId];

            // a relation is stored twice (in both directions), so the ID of the row that happens
            // to be processed first is not stable: the pair of pages is used as the identity instead
            var pairKey = GetPairKey(rel.SourceId, rel.DestinationId);

            if (rel.Type == RelationType.Spouse)
            {
                yield return new CalendarExportEventVM
                {
                    Uid = $"wedding-{pairKey}@bonsai",
                    Date = date,
                    Title = FormatTitle($"{source.Title} & {dest.Title}", Texts.CalendarPresenter_Wedding_Anniversary),
                    Url = GetPageUrl(baseUrl, GetEventPageKey(context, rel.EventId)),
                    RepeatsYearly = true,
                    // the marriage has ended: the anniversary is not celebrated anymore
                    RepeatsUntil = duration.RangeEnd is FuzzyDate end ? GetExactDate(end) : null
                };
            }
            else if (rel.Type is RelationType.Owner or RelationType.Pet)
            {
                if (GetExactDate(start) is not DateTime exact)
                    continue;

                var pet = rel.Type == RelationType.Pet ? source : dest;
                yield return new CalendarExportEventVM
                {
                    Uid = $"petadoption-{pairKey}@bonsai",
                    Date = exact,
                    Title = FormatTitle(pet.Title, Texts.CalendarPresenter_PetAdoption_Title),
                    Url = GetPageUrl(baseUrl, pet.Key)
                };
            }
            else if (rel.Type is RelationType.StepChild or RelationType.StepParent)
            {
                if (GetExactDate(start) is not DateTime exact)
                    continue;

                var child = rel.Type == RelationType.StepChild ? source : dest;
                var title = child.Gender == false
                    ? Texts.CalendarPresenter_ChildAdoptionF
                    : Texts.CalendarPresenter_ChildAdoptionM;

                yield return new CalendarExportEventVM
                {
                    Uid = $"childadoption-{pairKey}@bonsai",
                    Date = exact,
                    Title = FormatTitle(child.Title, title),
                    Url = GetPageUrl(baseUrl, child.Key)
                };
            }
        }
    }

    /// <summary>
    /// Returns the events described by pages of the Event type.
    /// </summary>
    private async Task<IReadOnlyList<CalendarExportEventVM>> GetOneTimeEventsAsync(string baseUrl)
    {
        var result = new List<CalendarExportEventVM>();
        var evtPages = await _db.Pages
                                .Where(x => x.Type == PageType.Event
                                            && x.IsDeleted == false
                                            && x.Facts.Contains("Main.Date"))
                                .Select(x => new { x.Id, x.Title, x.Key, x.Facts })
                                .ToListAsync();

        foreach (var evtPage in evtPages)
        {
            var facts = JObject.Parse(evtPage.Facts);
            var rawDate = facts["Main.Date"]?["Value"]?.ToString();

            if (FuzzyDate.TryParse(rawDate) is not { } date)
                continue;

            if (GetExactDate(date) is not DateTime exact)
                continue;

            result.Add(new CalendarExportEventVM
            {
                Uid = $"event-{evtPage.Id:N}@bonsai",
                Date = exact,
                Title = evtPage.Title,
                Url = GetPageUrl(baseUrl, evtPage.Key)
            });
        }

        return result;
    }

    /// <summary>
    /// Returns the date of the first occurrence of a yearly event, or null if the day is unknown.
    /// </summary>
    private static DateTime? GetAnniversaryDate(FuzzyDate date)
    {
        if (date.Day == null || date.Month == null)
            return null;

        // an unknown or approximate year only affects the starting point of the recurrence
        var year = date.IsDecade || date.Year == null ? DateTime.Now.Year : date.Year.Value;

        // February 29th exists in leap years only, so the recurrence must start in one
        if (date.Month == 2 && date.Day == 29)
            while (!DateTime.IsLeapYear(year))
                year++;

        return new DateTime(year, date.Month.Value, date.Day.Value);
    }

    /// <summary>
    /// Returns the date of a one-time event, or null if it is not known precisely.
    /// </summary>
    private static DateTime? GetExactDate(FuzzyDate date)
    {
        return date.IsPrecise
            ? new DateTime(date.Year.Value, date.Month.Value, date.Day.Value)
            : null;
    }

    /// <summary>
    /// Returns the absolute address of a page, if it is available.
    /// </summary>
    private static string GetPageUrl(string baseUrl, string key)
    {
        return key == null ? null : baseUrl + "/p/" + Uri.EscapeDataString(key);
    }

    /// <summary>
    /// Returns the identity of a pair of pages, regardless of the order of the relation.
    /// </summary>
    private static string GetPairKey(Guid first, Guid second)
    {
        return first.CompareTo(second) <= 0
            ? $"{first:N}-{second:N}"
            : $"{second:N}-{first:N}";
    }

    /// <summary>
    /// Returns the key of the page describing the relation's event, if it is available.
    /// </summary>
    private static string GetEventPageKey(RelationContext context, Guid? eventId)
    {
        if (eventId is not { } id)
            return null;

        return context.Pages.TryGetValue(id, out var page) ? page.Key : null;
    }

    /// <summary>
    /// Combines the name of the subject and the kind of the event into the event's title.
    /// </summary>
    private static string FormatTitle(string subject, string kind)
    {
        return string.Format(Texts.Calendar_Export_TitleFormat, subject, kind);
    }

    #endregion
}
