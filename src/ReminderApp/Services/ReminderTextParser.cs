using System.Globalization;
using System.Text.RegularExpressions;

namespace ReminderApp.Services;

public class ReminderTextParser
{
    // Longest-first unit patterns (critical for correct matching)
    private const string MinutesUnit = @"минут|минуты|минуту";
    private const string HoursUnit = @"часов|часа|час";
    private const string DaysUnit = @"дней|дня|день|суток|сутки";
    private const string WeeksUnit = @"недель|недели|неделю|неделя";
    private const string MonthsUnit = @"месяцев|месяца|месяц";
    private const string YearsUnit = @"лет|года|год";

    private const string AtTimeClause =
        @"(?:\s+в\s+(\d{1,2})(?::(\d{2}))?(?:\s+(?:часов|часа|час))?)?";

    private const string WeekdayNames =
        @"понедельник|вторник|среду|четверг|пятницу|субботу|воскресенье";

    private static readonly Regex ThroughPrefixRegex = new(
        @"^\s*через\s+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CompositeAtTimeRegex = new(
        @"^\s*в\s+(\d{1,2})(?::(\d{2}))?(?:\s+(?:часов|часа|час))?(?=\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SuffixDayOfMonthRegex = new(
        @"^(.+?)\s+(\d{1,2}(?:-?го)?\s+числа(?:\s+в\s+\d{1,2}(?::\d{2})?)?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InMinutesRegex = new(
        $@"^\s*через\s+(\d+)\s+({MinutesUnit})\b\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InMinuteSingularRegex = new(
        @"^\s*через\s+минуту\b\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InHoursRegex = new(
        $@"^\s*через\s+(\d+)\s+({HoursUnit})\b\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InHourSingularRegex = new(
        @"^\s*через\s+час\b\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex HalfHourRegex = new(
        @"^\s*через\s+пол\s*часа\b\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex PairRegex = new(
        $@"^\s*через\s+пару\s+({DaysUnit}|{WeeksUnit}|{MonthsUnit}|{YearsUnit})\b\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DayAfterTomorrowRegex = new(
        $@"^\s*(послезавтра|после\s+завтра){AtTimeClause}\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex OneDayRegex = new(
        $@"^\s*через\s+(?:день|сутки|1\s+день|1\s+сутки){AtTimeClause}\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InDaysRegex = new(
        $@"^\s*через\s+(\d+)\s+({DaysUnit})\b{AtTimeClause}\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InWeeksRegex = new(
        $@"^\s*через\s+(?:(\d+)\s+)?({WeeksUnit})\b{AtTimeClause}\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InMonthsRegex = new(
        $@"^\s*через\s+(?:(\d+)\s+)?({MonthsUnit})\b{AtTimeClause}\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex InYearsRegex = new(
        $@"^\s*через\s+(?:(\d+)\s+)?({YearsUnit})\b{AtTimeClause}\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WeekdayRegex = new(
        $@"^\s*(?:в|во)\s+({WeekdayNames})\b{AtTimeClause}\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TodayAtRegex = new(
        $@"^\s*сегодня\s+в\s+(\d{{1,2}})(?::(\d{{2}}))?(?:\s+(?:{HoursUnit}))?\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TomorrowAtRegex = new(
        $@"^\s*завтра\s+в\s+(\d{{1,2}})(?::(\d{{2}}))?(?:\s+(?:{HoursUnit}))?\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TomorrowBareRegex = new(
        @"^\s*завтра(?!\s+в\s*\d)(?:\s+(.+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TodayBareRegex = new(
        @"^\s*сегодня(?!\s+в\s*\d)(?:\s+(.+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex BareAtRegex = new(
        $@"^\s*в\s+(\d{{1,2}})(?::(\d{{2}}))?(?:\s+(?:{HoursUnit}))?\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex DayOfMonthRegex = new(
        $@"^\s*(\d{{1,2}})(?:-?го)?\s+числа(?:\s+в\s+(\d{{1,2}})(?::(\d{{2}}))?)?\s*(.*?)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly string[] SuffixAnchors =
    [
        " в понедельник", " во вторник", " в среду", " в четверг", " в пятницу", " в субботу", " в воскресенье",
        " через ", " послезавтра", " после завтра", " сегодня в ", " завтра в ", " завтра", " сегодня",
        " в "
    ];

    private enum CompositeSegmentKind
    {
        Minutes,
        Hours,
        Days,
        Weeks,
        Months,
        Years
    }

    private sealed record CompositeSegment(CompositeSegmentKind Kind, int Count);

    private static readonly Dictionary<string, DayOfWeek> WeekdayMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["понедельник"] = DayOfWeek.Monday,
            ["вторник"] = DayOfWeek.Tuesday,
            ["среду"] = DayOfWeek.Wednesday,
            ["четверг"] = DayOfWeek.Thursday,
            ["пятницу"] = DayOfWeek.Friday,
            ["субботу"] = DayOfWeek.Saturday,
            ["воскресенье"] = DayOfWeek.Sunday
        };

    private const int MaxMinutes = 525600;
    private const int MaxHours = 8760;
    private const int MaxDays = 3650;
    private const int MaxWeeks = 520;
    private const int MaxMonths = 120;
    private const int MaxYears = 20;

    private const string GenericParseError =
        "Не удалось понять время. Попробуйте: через 5 минут позвонить клиенту или завтра в 14:00 на почту";

    private const string TodayPastError =
        "Это время сегодня уже прошло. Напишите: завтра в 10:00 ...";

    private const string InvalidCountError =
        "Укажите целое число больше 0.";

    private const string TooManyDaysError =
        "Слишком большое число дней (максимум 3650).";

    private const string TooManyWeeksError =
        "Слишком большое число недель (максимум 520).";

    private const string TooManyMonthsError =
        "Слишком большое число месяцев (максимум 120).";

    private const string TooManyYearsError =
        "Слишком большое число лет (максимум 20).";

    private const string TooManyMinutesError =
        "Слишком большое число минут.";

    private const string TooManyHoursError =
        "Слишком большое число часов.";

    public ParseResult Parse(string input) => Parse(input, DateTime.Now);

    public ParseResult Parse(string input, DateTime now) => ParseInternal(input, now, allowSuffix: true);

    public bool TryParseDateTimeExpression(string expression, DateTime now, out DateTime nextRunAt, out string error)
    {
        var result = ParseInternal(expression, now, allowSuffix: false);
        if (result.IsSuccess && result.NextRunAt is not null)
        {
            nextRunAt = result.NextRunAt.Value;
            error = string.Empty;
            return true;
        }

        nextRunAt = default;
        error = result.ErrorMessage;
        return false;
    }

    private ParseResult ParseInternal(string input, DateTime now, bool allowSuffix)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return ParseResult.Failure(GenericParseError);
        }

        var text = input.Trim();

        var composite = TryParseCompositeThrough(text, now);
        if (composite is not null)
        {
            return composite;
        }

        var weekdayMatch = WeekdayRegex.Match(text);
        if (weekdayMatch.Success)
        {
            if (!WeekdayMap.TryGetValue(weekdayMatch.Groups[1].Value.Trim(), out var targetDay))
            {
                return ParseResult.Failure(GenericParseError);
            }

            var hasTime = weekdayMatch.Groups[2].Success;
            if (hasTime && !TryParseClockParts(weekdayMatch.Groups[2].Value, weekdayMatch.Groups[3], out var h, out var m))
            {
                return ParseResult.Failure(GenericParseError);
            }

            var hour = hasTime ? int.Parse(weekdayMatch.Groups[2].Value, CultureInfo.InvariantCulture) : now.Hour;
            var minute = hasTime && weekdayMatch.Groups[3].Success
                ? int.Parse(weekdayMatch.Groups[3].Value, CultureInfo.InvariantCulture)
                : hasTime ? 0 : now.Minute;

            var runAt = FindNextWeekdayOccurrence(now, targetDay, hour, minute, hasTime);
            var body = weekdayMatch.Groups[4].Value.Trim();
            return ParseResult.Success(runAt, ReminderText(body));
        }

        var minutesMatch = InMinutesRegex.Match(text);
        if (minutesMatch.Success)
        {
            var n = int.Parse(minutesMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (n <= 0)
            {
                return ParseResult.Failure(InvalidCountError);
            }

            if (n > MaxMinutes)
            {
                return ParseResult.Failure(TooManyMinutesError);
            }

            var body = minutesMatch.Groups[3].Value.Trim();
            return ParseResult.Success(now.AddMinutes(n), ReminderText(body));
        }

        var singularMinuteMatch = InMinuteSingularRegex.Match(text);
        if (singularMinuteMatch.Success)
        {
            var body = singularMinuteMatch.Groups[1].Value.Trim();
            return ParseResult.Success(now.AddMinutes(1), ReminderText(body));
        }

        var hoursMatch = InHoursRegex.Match(text);
        if (hoursMatch.Success)
        {
            var n = int.Parse(hoursMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (n <= 0)
            {
                return ParseResult.Failure(InvalidCountError);
            }

            if (n > MaxHours)
            {
                return ParseResult.Failure(TooManyHoursError);
            }

            var body = hoursMatch.Groups[3].Value.Trim();
            return ParseResult.Success(now.AddHours(n), ReminderText(body));
        }

        var singularHourMatch = InHourSingularRegex.Match(text);
        if (singularHourMatch.Success)
        {
            var body = singularHourMatch.Groups[1].Value.Trim();
            return ParseResult.Success(now.AddHours(1), ReminderText(body));
        }

        var halfHourMatch = HalfHourRegex.Match(text);
        if (halfHourMatch.Success)
        {
            var body = halfHourMatch.Groups[1].Value.Trim();
            return ParseResult.Success(now.AddMinutes(30), ReminderText(body));
        }

        var pairMatch = PairRegex.Match(text);
        if (pairMatch.Success)
        {
            var unit = pairMatch.Groups[1].Value.ToLowerInvariant();
            var body = pairMatch.Groups[2].Value.Trim();
            return TryPairUnit(now, unit, body);
        }

        var dayAfterMatch = DayAfterTomorrowRegex.Match(text);
        if (dayAfterMatch.Success)
        {
            var hasTime = dayAfterMatch.Groups[2].Success;
            var body = dayAfterMatch.Groups[4].Value.Trim();
            return TryRelativeCalendarDays(now, 2, hasTime, dayAfterMatch.Groups[2], dayAfterMatch.Groups[3], body);
        }

        var oneDayMatch = OneDayRegex.Match(text);
        if (oneDayMatch.Success)
        {
            var hasTime = oneDayMatch.Groups[2].Success;
            var body = oneDayMatch.Groups[4].Value.Trim();
            return TryRelativeCalendarDays(now, 1, hasTime, oneDayMatch.Groups[2], oneDayMatch.Groups[3], body);
        }

        var daysMatch = InDaysRegex.Match(text);
        if (daysMatch.Success)
        {
            var n = int.Parse(daysMatch.Groups[1].Value, CultureInfo.InvariantCulture);
            if (n <= 0)
            {
                return ParseResult.Failure(InvalidCountError);
            }

            if (n > MaxDays)
            {
                return ParseResult.Failure(TooManyDaysError);
            }

            var hasTime = daysMatch.Groups[3].Success;
            var body = daysMatch.Groups[5].Value.Trim();
            return TryRelativeCalendarDays(now, n, hasTime, daysMatch.Groups[3], daysMatch.Groups[4], body);
        }

        var weeksMatch = InWeeksRegex.Match(text);
        if (weeksMatch.Success)
        {
            var n = weeksMatch.Groups[1].Success ? int.Parse(weeksMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
            if (n <= 0)
            {
                return ParseResult.Failure(InvalidCountError);
            }

            if (n > MaxWeeks)
            {
                return ParseResult.Failure(TooManyWeeksError);
            }

            var hasTime = weeksMatch.Groups[3].Success;
            var body = weeksMatch.Groups[5].Value.Trim();
            return TryRelativeCalendarDays(now, 7 * n, hasTime, weeksMatch.Groups[3], weeksMatch.Groups[4], body);
        }

        var monthsMatch = InMonthsRegex.Match(text);
        if (monthsMatch.Success)
        {
            var n = monthsMatch.Groups[1].Success ? int.Parse(monthsMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
            if (n <= 0)
            {
                return ParseResult.Failure(InvalidCountError);
            }

            if (n > MaxMonths)
            {
                return ParseResult.Failure(TooManyMonthsError);
            }

            var hasTime = monthsMatch.Groups[3].Success;
            var body = monthsMatch.Groups[5].Value.Trim();
            return TryRelativeMonths(now, n, hasTime, monthsMatch.Groups[3], monthsMatch.Groups[4], body);
        }

        var yearsMatch = InYearsRegex.Match(text);
        if (yearsMatch.Success)
        {
            var n = yearsMatch.Groups[1].Success ? int.Parse(yearsMatch.Groups[1].Value, CultureInfo.InvariantCulture) : 1;
            if (n <= 0)
            {
                return ParseResult.Failure(InvalidCountError);
            }

            if (n > MaxYears)
            {
                return ParseResult.Failure(TooManyYearsError);
            }

            var hasTime = yearsMatch.Groups[3].Success;
            var body = yearsMatch.Groups[5].Value.Trim();
            return TryRelativeYears(now, n, hasTime, yearsMatch.Groups[3], yearsMatch.Groups[4], body);
        }

        var todayMatch = TodayAtRegex.Match(text);
        if (todayMatch.Success)
        {
            if (!TryParseClockParts(todayMatch.Groups[1].Value, todayMatch.Groups[2], out var h, out var m))
            {
                return ParseResult.Failure(GenericParseError);
            }

            var body = todayMatch.Groups[3].Value.Trim();
            var reminderText = ReminderText(body);
            var runAt = CombineTodayTime(h, m);
            if (runAt <= now)
            {
                return ParseResult.Failure(TodayPastError);
            }

            return ParseResult.Success(runAt, reminderText);
        }

        var tomorrowAtMatch = TomorrowAtRegex.Match(text);
        if (tomorrowAtMatch.Success)
        {
            if (!TryParseClockParts(tomorrowAtMatch.Groups[1].Value, tomorrowAtMatch.Groups[2], out var h, out var m))
            {
                return ParseResult.Failure(GenericParseError);
            }

            var body = tomorrowAtMatch.Groups[3].Value.Trim();
            var reminderText = ReminderText(body);
            var runAt = CombineTomorrowTime(h, m);
            return ParseResult.Success(runAt, reminderText);
        }

        var tomorrowBareMatch = TomorrowBareRegex.Match(text);
        if (tomorrowBareMatch.Success)
        {
            var body = tomorrowBareMatch.Groups[1].Success ? tomorrowBareMatch.Groups[1].Value.Trim() : string.Empty;
            return ParseResult.Success(now.AddDays(1), ReminderText(body));
        }

        var todayBareMatch = TodayBareRegex.Match(text);
        if (todayBareMatch.Success)
        {
            var body = todayBareMatch.Groups[1].Success ? todayBareMatch.Groups[1].Value.Trim() : string.Empty;
            return ParseResult.Success(now, ReminderText(body));
        }

        var bareMatch = BareAtRegex.Match(text);
        if (bareMatch.Success)
        {
            if (!TryParseClockParts(bareMatch.Groups[1].Value, bareMatch.Groups[2], out var h, out var m))
            {
                return ParseResult.Failure(GenericParseError);
            }

            var body = bareMatch.Groups[3].Value.Trim();
            var reminderText = ReminderText(body);
            var todayAt = CombineTodayTime(h, m);
            var runAt = todayAt > now ? todayAt : CombineTomorrowTime(h, m);
            return ParseResult.Success(runAt, reminderText);
        }

        var dayOfMonthMatch = DayOfMonthRegex.Match(text);
        if (dayOfMonthMatch.Success)
        {
            if (!int.TryParse(dayOfMonthMatch.Groups[1].Value, out var day) || day is < 1 or > 31)
            {
                return ParseResult.Failure("Число месяца должно быть от 1 до 31.");
            }

            var hasTime = dayOfMonthMatch.Groups[2].Success;
            var hour = now.Hour;
            var minute = now.Minute;
            var second = hasTime ? 0 : now.Second;
            if (hasTime)
            {
                if (!TryParseClockParts(dayOfMonthMatch.Groups[2].Value, dayOfMonthMatch.Groups[3], out hour, out minute))
                {
                    return ParseResult.Failure(GenericParseError);
                }
            }

            var runAt = FindNearestMonthDay(now, day, hour, minute, second);
            var body = dayOfMonthMatch.Groups[4].Value.Trim();
            return ParseResult.Success(runAt, ReminderText(body));
        }

        if (allowSuffix && TryParseSuffixOrder(text, now, out var suffixResult))
        {
            return suffixResult;
        }

        return ParseResult.Failure(GenericParseError);
    }

    private ParseResult? TryParseCompositeThrough(string text, DateTime now)
    {
        var prefixMatch = ThroughPrefixRegex.Match(text);
        if (!prefixMatch.Success)
        {
            return null;
        }

        var remainder = text[prefixMatch.Length..];
        var segments = new List<CompositeSegment>();
        CompositeSegmentKind? lastCalendarKind = null;
        var calendarCount = 0;

        while (TryConsumeCompositeSegment(ref remainder, out var segment))
        {
            segments.Add(segment);
            if (segment.Kind is CompositeSegmentKind.Days or CompositeSegmentKind.Weeks
                or CompositeSegmentKind.Months or CompositeSegmentKind.Years)
            {
                lastCalendarKind = segment.Kind;
                calendarCount = segment.Count;
            }
        }

        if (segments.Count == 0)
        {
            return null;
        }

        remainder = remainder.TrimStart();
        var atTimeMatch = CompositeAtTimeRegex.Match(remainder);
        var hasAtTime = atTimeMatch.Success;
        int clockHour = 0;
        int clockMinute = 0;

        if (hasAtTime)
        {
            if (!TryParseClockParts(atTimeMatch.Groups[1].Value, atTimeMatch.Groups[2], out clockHour, out clockMinute))
            {
                return ParseResult.Failure(GenericParseError);
            }

            if (lastCalendarKind is null)
            {
                return null;
            }

            remainder = remainder[atTimeMatch.Length..].TrimStart();
        }

        var body = remainder.Trim();

        if (hasAtTime && lastCalendarKind is not null)
        {
            var runAt = ApplyCalendarAnchorWithTime(now, lastCalendarKind.Value, calendarCount, clockHour, clockMinute);
            return ParseResult.Success(runAt, ReminderText(body));
        }

        var additive = now;
        foreach (var segment in segments)
        {
            var err = ApplyAdditiveSegment(ref additive, segment);
            if (err is not null)
            {
                return ParseResult.Failure(err);
            }
        }

        return ParseResult.Success(additive, ReminderText(body));
    }

    private static bool TryConsumeCompositeSegment(ref string remainder, out CompositeSegment segment)
    {
        segment = default!;
        remainder = remainder.TrimStart();

        var patterns = new (string Pattern, CompositeSegmentKind Kind, bool OptionalCount)[]
        {
            ($@"^(\d+)\s+({MinutesUnit})\b", CompositeSegmentKind.Minutes, false),
            (@"^минуту\b", CompositeSegmentKind.Minutes, true),
            ($@"^(\d+)\s+({HoursUnit})\b", CompositeSegmentKind.Hours, false),
            (@"^пол\s*часа\b", CompositeSegmentKind.Hours, true),
            (@"^час\b", CompositeSegmentKind.Hours, true),
            ($@"^(\d+)\s+({DaysUnit})\b", CompositeSegmentKind.Days, false),
            (@"^(?:день|сутки|1\s+день|1\s+сутки)\b", CompositeSegmentKind.Days, true),
            ($@"^(?:(\d+)\s+)?({WeeksUnit})\b", CompositeSegmentKind.Weeks, true),
            ($@"^(?:(\d+)\s+)?({MonthsUnit})\b", CompositeSegmentKind.Months, true),
            ($@"^(?:(\d+)\s+)?({YearsUnit})\b", CompositeSegmentKind.Years, true),
            ($@"^пару\s+({DaysUnit}|{WeeksUnit}|{MonthsUnit}|{YearsUnit})\b", CompositeSegmentKind.Days, true)
        };

        foreach (var (pattern, kind, optionalCount) in patterns)
        {
            var rx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var m = rx.Match(remainder);
            if (!m.Success)
            {
                continue;
            }

            var segmentKind = kind;
            var count = 1;
            if (pattern.StartsWith("^пару", StringComparison.Ordinal))
            {
                var unit = m.Groups[1].Value.ToLowerInvariant();
                count = 2;
                segmentKind = unit switch
                {
                    var u when u.Contains("недел") => CompositeSegmentKind.Weeks,
                    var u when u.Contains("месяц") => CompositeSegmentKind.Months,
                    var u when u.Contains("год") || u.Contains("лет") => CompositeSegmentKind.Years,
                    _ => CompositeSegmentKind.Days
                };
            }
            else if (m.Groups[1].Success && int.TryParse(m.Groups[1].Value, out var parsed))
            {
                count = parsed;
            }
            else if (!optionalCount)
            {
                continue;
            }

            if (count <= 0)
            {
                continue;
            }

            if (segmentKind == CompositeSegmentKind.Hours && pattern.Contains("пол"))
            {
                segment = new CompositeSegment(CompositeSegmentKind.Minutes, 30);
            }
            else
            {
                segment = new CompositeSegment(segmentKind, count);
            }

            remainder = remainder[m.Length..];
            return true;
        }

        return false;
    }

    private static string? ApplyAdditiveSegment(ref DateTime runAt, CompositeSegment segment)
    {
        switch (segment.Kind)
        {
            case CompositeSegmentKind.Minutes:
                if (segment.Count > MaxMinutes)
                {
                    return TooManyMinutesError;
                }

                runAt = runAt.AddMinutes(segment.Count);
                return null;
            case CompositeSegmentKind.Hours:
                if (segment.Count > MaxHours)
                {
                    return TooManyHoursError;
                }

                runAt = runAt.AddHours(segment.Count);
                return null;
            case CompositeSegmentKind.Days:
                if (segment.Count > MaxDays)
                {
                    return TooManyDaysError;
                }

                runAt = runAt.AddDays(segment.Count);
                return null;
            case CompositeSegmentKind.Weeks:
                if (segment.Count > MaxWeeks)
                {
                    return TooManyWeeksError;
                }

                runAt = runAt.AddDays(7 * segment.Count);
                return null;
            case CompositeSegmentKind.Months:
                if (segment.Count > MaxMonths)
                {
                    return TooManyMonthsError;
                }

                runAt = runAt.AddMonths(segment.Count);
                return null;
            case CompositeSegmentKind.Years:
                if (segment.Count > MaxYears)
                {
                    return TooManyYearsError;
                }

                runAt = runAt.AddYears(segment.Count);
                return null;
            default:
                return GenericParseError;
        }
    }

    private static DateTime ApplyCalendarAnchorWithTime(
        DateTime now,
        CompositeSegmentKind kind,
        int count,
        int hour,
        int minute)
    {
        DateTime anchor = kind switch
        {
            CompositeSegmentKind.Days => now.Date.AddDays(count),
            CompositeSegmentKind.Weeks => now.Date.AddDays(7 * count),
            CompositeSegmentKind.Months => now.AddMonths(count).Date,
            CompositeSegmentKind.Years => now.AddYears(count).Date,
            _ => now.Date.AddDays(count)
        };

        return new DateTime(anchor.Year, anchor.Month, anchor.Day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private bool TryParseSuffixOrder(string fullInput, DateTime now, out ParseResult result)
    {
        var dayOfMonthSuffix = SuffixDayOfMonthRegex.Match(fullInput);
        if (dayOfMonthSuffix.Success)
        {
            var reminderText = dayOfMonthSuffix.Groups[1].Value.Trim();
            var expression = dayOfMonthSuffix.Groups[2].Value.Trim();
            var dtResult = ParseInternal(expression, now, allowSuffix: false);
            if (dtResult.IsSuccess && dtResult.NextRunAt is not null)
            {
                result = ParseResult.Success(dtResult.NextRunAt.Value, reminderText);
                return true;
            }
        }

        foreach (var anchor in SuffixAnchors)
        {
            var searchStart = fullInput.Length;
            while (searchStart > 0)
            {
                var idx = fullInput.LastIndexOf(anchor, searchStart - 1, StringComparison.OrdinalIgnoreCase);
                if (idx <= 0)
                {
                    break;
                }

                var reminderText = fullInput[..idx].Trim();
                var expression = fullInput[idx..].Trim();
                if (!string.IsNullOrWhiteSpace(reminderText) && !string.IsNullOrWhiteSpace(expression))
                {
                    var dtResult = ParseInternal(expression, now, allowSuffix: false);
                    if (dtResult.IsSuccess && dtResult.NextRunAt is not null)
                    {
                        result = ParseResult.Success(dtResult.NextRunAt.Value, reminderText);
                        return true;
                    }
                }

                searchStart = idx;
            }
        }

        result = ParseResult.Failure(GenericParseError);
        return false;
    }

    private static ParseResult TryPairUnit(DateTime now, string unit, string body)
    {
        var t = unit.ToLowerInvariant();
        if (t is "дней" or "дня")
        {
            return ParseResult.Success(now.AddDays(2), ReminderText(body));
        }

        if (t is "недель" or "недели")
        {
            return ParseResult.Success(now.AddDays(14), ReminderText(body));
        }

        if (t is "месяцев" or "месяца")
        {
            return ParseResult.Success(now.AddMonths(2), ReminderText(body));
        }

        if (t is "лет" or "года")
        {
            return ParseResult.Success(now.AddYears(2), ReminderText(body));
        }

        return ParseResult.Failure(GenericParseError);
    }

    private static ParseResult TryRelativeCalendarDays(
        DateTime now,
        int dayDelta,
        bool hasExplicitTime,
        Group hourGroup,
        Group minuteGroup,
        string body)
    {
        if (hasExplicitTime)
        {
            if (!TryParseClockParts(hourGroup.Value, minuteGroup, out var h, out var m))
            {
                return ParseResult.Failure(GenericParseError);
            }

            var day = now.Date.AddDays(dayDelta);
            var runAt = new DateTime(day.Year, day.Month, day.Day, h, m, 0, DateTimeKind.Unspecified);
            return ParseResult.Success(runAt, ReminderText(body));
        }

        return ParseResult.Success(now.AddDays(dayDelta), ReminderText(body));
    }

    private static ParseResult TryRelativeMonths(
        DateTime now,
        int months,
        bool hasExplicitTime,
        Group hourGroup,
        Group minuteGroup,
        string body)
    {
        var at = now.AddMonths(months);
        if (!hasExplicitTime)
        {
            return ParseResult.Success(at, ReminderText(body));
        }

        if (!TryParseClockParts(hourGroup.Value, minuteGroup, out var h, out var m))
        {
            return ParseResult.Failure(GenericParseError);
        }

        var runAt = new DateTime(at.Year, at.Month, at.Day, h, m, 0, DateTimeKind.Unspecified);
        return ParseResult.Success(runAt, ReminderText(body));
    }

    private static ParseResult TryRelativeYears(
        DateTime now,
        int years,
        bool hasExplicitTime,
        Group hourGroup,
        Group minuteGroup,
        string body)
    {
        var at = now.AddYears(years);
        if (!hasExplicitTime)
        {
            return ParseResult.Success(at, ReminderText(body));
        }

        if (!TryParseClockParts(hourGroup.Value, minuteGroup, out var h, out var m))
        {
            return ParseResult.Failure(GenericParseError);
        }

        var runAt = new DateTime(at.Year, at.Month, at.Day, h, m, 0, DateTimeKind.Unspecified);
        return ParseResult.Success(runAt, ReminderText(body));
    }

    private static DateTime FindNextWeekdayOccurrence(
        DateTime now,
        DayOfWeek targetDay,
        int hour,
        int minute,
        bool hasExplicitTime)
    {
        var today = now.Date;
        var daysUntil = ((int)targetDay - (int)today.DayOfWeek + 7) % 7;

        if (daysUntil == 0)
        {
            if (!hasExplicitTime)
            {
                return today.AddDays(7).AddHours(now.Hour).AddMinutes(now.Minute).AddSeconds(now.Second);
            }

            var candidate = new DateTime(today.Year, today.Month, today.Day, hour, minute, 0, DateTimeKind.Unspecified);
            if (candidate > now)
            {
                return candidate;
            }

            return today.AddDays(7).AddHours(hour).AddMinutes(minute);
        }

        var targetDate = today.AddDays(daysUntil);
        if (hasExplicitTime)
        {
            return new DateTime(targetDate.Year, targetDate.Month, targetDate.Day, hour, minute, 0, DateTimeKind.Unspecified);
        }

        return targetDate.AddHours(now.Hour).AddMinutes(now.Minute).AddSeconds(now.Second);
    }

    private static string ReminderText(string body) =>
        string.IsNullOrWhiteSpace(body) ? "Напоминание" : body.Trim();

    private static DateTime CombineTodayTime(int hour, int minute)
    {
        var d = DateTime.Today;
        return new DateTime(d.Year, d.Month, d.Day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private static DateTime CombineTomorrowTime(int hour, int minute)
    {
        var d = DateTime.Today.AddDays(1);
        return new DateTime(d.Year, d.Month, d.Day, hour, minute, 0, DateTimeKind.Unspecified);
    }

    private static bool TryParseClockParts(string hourText, Group minuteGroup, out int hour, out int minute)
    {
        hour = 0;
        minute = 0;
        if (!int.TryParse(hourText, out hour) || hour is < 0 or > 23)
        {
            return false;
        }

        if (minuteGroup.Success && !string.IsNullOrEmpty(minuteGroup.Value))
        {
            if (!int.TryParse(minuteGroup.Value, out minute) || minute is < 0 or > 59)
            {
                return false;
            }
        }

        return true;
    }

    private static DateTime FindNearestMonthDay(DateTime now, int day, int hour, int minute, int second)
    {
        var cursor = new DateTime(now.Year, now.Month, 1, hour, minute, second, DateTimeKind.Unspecified);
        for (var i = 0; i < 24; i++)
        {
            var daysInMonth = DateTime.DaysInMonth(cursor.Year, cursor.Month);
            if (day <= daysInMonth)
            {
                var candidate = new DateTime(cursor.Year, cursor.Month, day, hour, minute, second, DateTimeKind.Unspecified);
                if (candidate > now)
                {
                    return candidate;
                }
            }

            cursor = cursor.AddMonths(1);
        }

        return now.AddMonths(1);
    }
}
