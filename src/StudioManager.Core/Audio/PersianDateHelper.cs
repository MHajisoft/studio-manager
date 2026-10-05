using System.Globalization;

namespace StudioManager.Core.Audio;

public static class PersianDateHelper
{
    private static readonly PersianCalendar Calendar = new();

    public static string GetPersianDate(DateTime date) =>
        $"{Calendar.GetDayOfMonth(date):D2}-{Calendar.GetMonth(date):D2}-{Calendar.GetYear(date)}";

    public static uint GetPersianYear(DateTime date) => (uint)Calendar.GetYear(date);
}
