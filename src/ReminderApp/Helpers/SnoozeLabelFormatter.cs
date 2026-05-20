namespace ReminderApp.Helpers;

public static class SnoozeLabelFormatter
{
    public static string FormatCompact(int minutes)
    {
        if (minutes < 60)
        {
            return $"{minutes} мин";
        }

        if (minutes % 60 == 0 && minutes < 1440)
        {
            return $"{minutes / 60} ч";
        }

        return Format(minutes);
    }

    public static string Format(int minutes)
    {
        if (minutes <= 0)
        {
            return $"{minutes} мин";
        }

        if (minutes < 60)
        {
            return minutes + " " + PluralRu(minutes, "минута", "минуты", "минут");
        }

        if (minutes < 1440)
        {
            var h = minutes / 60;
            if (minutes % 60 != 0)
            {
                return minutes + " " + PluralRu(minutes, "минута", "минуты", "минут");
            }

            return h + " " + PluralRu(h, "час", "часа", "часов");
        }

        var d = minutes / 1440;
        if (minutes % 1440 != 0)
        {
            return minutes + " " + PluralRu(minutes, "минута", "минуты", "минут");
        }

        return d + " " + PluralRu(d, "день", "дня", "дней");
    }

    private static string PluralRu(int n, string one, string few, string many)
    {
        var a = Math.Abs(n) % 100;
        var b = a % 10;
        if (a is > 10 and < 20)
        {
            return many;
        }

        return b switch
        {
            1 => one,
            2 or 3 or 4 => few,
            _ => many
        };
    }
}
