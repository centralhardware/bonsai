// Converts dates rendered by the LocalDate tag helper to the visitor's browser timezone and locale.
// Elements are marked with [data-local-date] (the value is the display mode) and carry a UTC
// instant in their `datetime` attribute. The server-rendered text is a no-JS fallback.
$(function () {
    var HUMANIZE_OR_DATE_THRESHOLD_DAYS = 14;

    var locale = (document.documentElement.lang || 'en');

    var absoluteFormats = {
        // dd MMMM yyyy
        short: { day: 'numeric', month: 'long', year: 'numeric' },
        // general date + long time (.NET "G")
        full: { year: 'numeric', month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit', second: '2-digit' },
        // full date + short time (.NET "f")
        changeset: { weekday: 'long', year: 'numeric', month: 'long', day: 'numeric', hour: '2-digit', minute: '2-digit' }
    };

    var relativeFormat = (typeof Intl !== 'undefined' && Intl.RelativeTimeFormat)
        ? new Intl.RelativeTimeFormat(locale, { numeric: 'auto' })
        : null;

    var relativeUnits = [
        ['year', 31536000],
        ['month', 2592000],
        ['week', 604800],
        ['day', 86400],
        ['hour', 3600],
        ['minute', 60],
        ['second', 1]
    ];

    function formatAbsolute(date, format) {
        return new Intl.DateTimeFormat(locale, absoluteFormats[format]).format(date);
    }

    function humanize(date) {
        var diffSeconds = (date.getTime() - Date.now()) / 1000;
        if (!relativeFormat) {
            return formatAbsolute(date, 'changeset');
        }

        for (var i = 0; i < relativeUnits.length; i++) {
            var unit = relativeUnits[i][0];
            var size = relativeUnits[i][1];
            if (Math.abs(diffSeconds) >= size || unit === 'second') {
                return relativeFormat.format(Math.round(diffSeconds / size), unit);
            }
        }
    }

    function capitalize(text) {
        return text.length ? text.charAt(0).toUpperCase() + text.slice(1) : text;
    }

    function render($el) {
        var iso = $el.attr('datetime');
        if (!iso) {
            return;
        }

        var date = new Date(iso);
        if (isNaN(date.getTime())) {
            return;
        }

        var mode = $el.attr('data-local-date');
        switch (mode) {
            case 'inline':
                $el.text(formatAbsolute(date, 'changeset') + ' (' + humanize(date) + ')');
                break;

            case 'humanizeOrDate':
                var isOld = date.getTime() < Date.now() - HUMANIZE_OR_DATE_THRESHOLD_DAYS * 86400 * 1000;
                $el.text(capitalize(isOld ? formatAbsolute(date, 'short') : humanize(date)));
                $el.attr('title', formatAbsolute(date, 'full'));
                break;

            default: // humanize
                $el.text(humanize(date));
                $el.attr('title', formatAbsolute(date, 'full'));
                break;
        }
    }

    $('[data-local-date]').each(function () {
        render($(this));
    });
});
