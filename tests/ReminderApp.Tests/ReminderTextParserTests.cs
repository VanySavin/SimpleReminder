using ReminderApp.Services;
using Xunit;

namespace ReminderApp.Tests;

public class ReminderTextParserTests
{
    private static readonly DateTime FixedNow = new(2026, 5, 20, 10, 0, 0, DateTimeKind.Unspecified); // Wednesday

    private readonly ReminderTextParser _parser = new();

    [Theory]
    [InlineData("через 5 часов суп", "суп")]
    [InlineData("через 4 часа суп", "суп")]
    [InlineData("через 1 час суп", "суп")]
    [InlineData("через 5 минут суп", "суп")]
    [InlineData("через 1 минуту суп", "суп")]
    public void UnitEndings_DoNotLeakIntoText(string input, string expectedText)
    {
        var result = _parser.Parse(input, FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedText, result.ReminderText);
    }

    [Fact]
    public void CompositeTime_HourAndMinutes()
    {
        var result = _parser.Parse("через 1 час 36 минут суп", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(FixedNow.AddHours(1).AddMinutes(36), result.NextRunAt);
        Assert.Equal("суп", result.ReminderText);
    }

    [Fact]
    public void CompositeTime_DayAndHours()
    {
        var result = _parser.Parse("через 1 день 2 часа суп", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(FixedNow.AddDays(1).AddHours(2), result.NextRunAt);
        Assert.Equal("суп", result.ReminderText);
    }

    [Fact]
    public void RelativeDateWithClock_DaysAtHour()
    {
        var result = _parser.Parse("через 4 дня в 12 часов суп", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 5, 24, 12, 0, 0), result.NextRunAt);
        Assert.Equal("суп", result.ReminderText);
    }

    [Fact]
    public void RelativeDateWithClock_DaysAtHourMinutes()
    {
        var result = _parser.Parse("через 4 дня в 12:30 суп", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 5, 24, 12, 30, 0), result.NextRunAt);
    }

    [Fact]
    public void RelativeDateWithClock_WeekAtHour()
    {
        var result = _parser.Parse("через неделю в 23 часа суп", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 5, 27, 23, 0, 0), result.NextRunAt);
        Assert.Equal("суп", result.ReminderText);
    }

    [Fact]
    public void Weekday_NearestMondayFromWednesday()
    {
        var result = _parser.Parse("в понедельник суп", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 5, 25, 10, 0, 0), result.NextRunAt);
        Assert.Equal("суп", result.ReminderText);
    }

    [Fact]
    public void Weekday_TodayWithFutureTime()
    {
        var mondayMorning = new DateTime(2026, 5, 18, 10, 0, 0);
        var result = _parser.Parse("в понедельник в 23 суп", mondayMorning);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 5, 18, 23, 0, 0), result.NextRunAt);
    }

    [Fact]
    public void Weekday_TodayWithPastTimeGoesToNextWeek()
    {
        var mondayLate = new DateTime(2026, 5, 18, 23, 30, 0);
        var result = _parser.Parse("в понедельник в 23 суп", mondayLate);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 5, 25, 23, 0, 0), result.NextRunAt);
    }

    [Theory]
    [InlineData("суп через 1 час 36 минут", "суп", 96)]
    [InlineData("суп через 4 дня в 12 часов", "суп", 0)]
    [InlineData("суп в понедельник", "суп", 0)]
    public void SuffixOrder_ParsesNewForms(string input, string expectedText, int expectedMinuteOffset)
    {
        var result = _parser.Parse(input, FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(expectedText, result.ReminderText);
        if (expectedMinuteOffset > 0)
        {
            Assert.Equal(FixedNow.AddMinutes(expectedMinuteOffset), result.NextRunAt);
        }
    }

    [Fact]
    public void SuffixOrder_WeekdayWithTime()
    {
        var result = _parser.Parse("суп в понедельник в 23 часа", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal("суп", result.ReminderText);
        Assert.Equal(new DateTime(2026, 5, 25, 23, 0, 0), result.NextRunAt);
    }

    [Fact]
    public void Weekday_PrefixWithHourFromWednesday()
    {
        var result = _parser.Parse("в понедельник в 23 часа суп", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal(new DateTime(2026, 5, 25, 23, 0, 0), result.NextRunAt);
        Assert.Equal("суп", result.ReminderText);
    }

    [Fact]
    public void SuffixOrder_LongTextBeforeThroughMinutes()
    {
        var result = _parser.Parse("позвонить жене через 5 минут", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal("позвонить жене", result.ReminderText);
        Assert.Equal(FixedNow.AddMinutes(5), result.NextRunAt);
    }

    [Fact]
    public void SuffixOrder_DaysAtHourWithExpectedTime()
    {
        var result = _parser.Parse("суп через 4 дня в 12 часов", FixedNow);
        Assert.True(result.IsSuccess);
        Assert.Equal("суп", result.ReminderText);
        Assert.Equal(new DateTime(2026, 5, 24, 12, 0, 0), result.NextRunAt);
    }

    [Theory]
    [InlineData("через минуту суп")]
    [InlineData("через час суп")]
    [InlineData("завтра суп")]
    [InlineData("завтра в 9 суп")]
    [InlineData("послезавтра суп")]
    [InlineData("через месяц суп")]
    [InlineData("20 числа суп")]
    [InlineData("суп через 5 минут")]
    [InlineData("суп завтра")]
    [InlineData("суп 20 числа")]
    public void Regression_ExistingFormsStillParse(string input)
    {
        var result = _parser.Parse(input, FixedNow);
        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.NotNull(result.NextRunAt);
    }
}
