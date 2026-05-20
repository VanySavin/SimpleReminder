using System.Text.RegularExpressions;

namespace ReminderApp.Services;

public static class SnoozeTextNormalizer
{
    private static readonly Regex MinuteWordRegex = new(
        @"^\s*(?:отлож(?:и|ить)\s+)?на\s+минут(?:к[ау]|у)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex HourWordRegex = new(
        @"^\s*(?:отлож(?:и|ить)\s+)?на\s+час\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex UnitWordRegex = new(
        @"^\s*(?:отлож(?:и|ить)\s+)?на\s+(день|сутки|неделю|месяц|год)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex NumericUnitRegex = new(
        @"^\s*(?:отлож(?:и|ить)\s+)?на\s+(\d+)\s+(минут[аы]?|минут|час(?:а|ов)?|дн(?:я|ей)|день|сут(?:ки|ок)|недел(?:ю|и|ь)|месяц(?:а|ев)?|год(?:а|ов)?|лет)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string Normalize(string input)
    {
        var text = input?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return text;
        }

        if (MinuteWordRegex.IsMatch(text))
        {
            return "через минуту";
        }

        if (HourWordRegex.IsMatch(text))
        {
            return "через час";
        }

        var unitWord = UnitWordRegex.Match(text);
        if (unitWord.Success)
        {
            return "через " + unitWord.Groups[1].Value.ToLowerInvariant();
        }

        var numeric = NumericUnitRegex.Match(text);
        if (!numeric.Success)
        {
            return text;
        }

        var count = numeric.Groups[1].Value;
        var unit = NormalizeUnit(numeric.Groups[2].Value);
        return $"через {count} {unit}";
    }

    private static string NormalizeUnit(string raw)
    {
        var u = raw.ToLowerInvariant();
        if (u.StartsWith("минут"))
        {
            return "минут";
        }

        if (u.StartsWith("час"))
        {
            return "часов";
        }

        if (u.StartsWith("дн") || u == "день")
        {
            return "дней";
        }

        if (u.StartsWith("сут"))
        {
            return "суток";
        }

        if (u.StartsWith("недел"))
        {
            return "недель";
        }

        if (u.StartsWith("месяц"))
        {
            return "месяцев";
        }

        if (u.StartsWith("год") || u == "лет")
        {
            return "лет";
        }

        return raw;
    }
}
