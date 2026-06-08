using System;
using Bonsai.Localization;
using Humanizer;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Bonsai.Code.Utils.Helpers;

/// <summary>
/// Display modes for the <see cref="LocalDateTagHelper"/>.
/// </summary>
public enum LocalDateMode
{
    /// <summary>
    /// Body shows the relative ("5 minutes ago") form, the full date is shown as a tooltip.
    /// </summary>
    Humanize,

    /// <summary>
    /// Body shows the absolute date followed by the relative form in parentheses.
    /// </summary>
    Inline,

    /// <summary>
    /// Body shows the relative form for recent dates and an absolute short date for old ones; full date as a tooltip.
    /// </summary>
    HumanizeOrDate
}

/// <summary>
/// Renders a date that is converted to the visitor's browser timezone on the client side.
/// The server-rendered content acts as a no-JS fallback.
/// </summary>
[HtmlTargetElement("local-date", Attributes = "date")]
public class LocalDateTagHelper : TagHelper
{
    /// <summary>
    /// Number of days after which <see cref="LocalDateMode.HumanizeOrDate"/> switches to an absolute date.
    /// </summary>
    private const int HumanizeOrDateThresholdDays = 14;

    [HtmlAttributeName("date")]
    public DateTimeOffset Date { get; set; }

    [HtmlAttributeName("mode")]
    public LocalDateMode Mode { get; set; } = LocalDateMode.Humanize;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "time";

        var local = Date.ToLocalTime();
        var relative = Date.Humanize();

        string body;
        string mode;
        switch (Mode)
        {
            case LocalDateMode.Inline:
                mode = "inline";
                body = $"{local.ToString(Texts.DateFormat_Changeset)} ({relative})";
                break;

            case LocalDateMode.HumanizeOrDate:
                mode = "humanizeOrDate";
                body = (Date < DateTimeOffset.Now.AddDays(-HumanizeOrDateThresholdDays)
                    ? local.LocalDateTime.ToLocalizedShortDate()
                    : relative).Capitalize();
                output.Attributes.SetAttribute("title", local.LocalDateTime.ToLocalizedFullDate());
                break;

            default:
                mode = "humanize";
                body = relative;
                output.Attributes.SetAttribute("title", local.LocalDateTime.ToLocalizedFullDate());
                break;
        }

        output.Attributes.SetAttribute("datetime", Date.ToUniversalTime().ToString("o"));
        output.Attributes.SetAttribute("data-local-date", mode);
        output.Content.SetContent(body);
    }
}
