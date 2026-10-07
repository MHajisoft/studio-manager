using System.Globalization;

namespace StudioManager.Core.Audio;

/// <summary>
/// Formats dates for file names and tags using the active UI culture
/// (Persian calendar for fa-*, Gregorian otherwise).
/// </summary>
public static class StudioDateHelper
{
    private static readonly PersianCalendar Persian = new();

    public static string FormatDate(DateTime date, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        if (IsPersian(culture))
            return $"{Persian.GetDayOfMonth(date):D2}-{Persian.GetMonth(date):D2}-{Persian.GetYear(date)}";

        return date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    public static uint FormatYear(DateTime date, CultureInfo? culture = null)
    {
        culture ??= CultureInfo.CurrentCulture;
        return IsPersian(culture)
            ? (uint)Persian.GetYear(date)
            : (uint)date.Year;
    }

    private static bool IsPersian(CultureInfo culture) =>
        culture.Name.StartsWith("fa", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Legacy name kept for call-site clarity during migration.</summary>
public static class PersianDateHelper
{
    public static string GetPersianDate(DateTime date) => StudioDateHelper.FormatDate(date);

    public static uint GetPersianYear(DateTime date) => StudioDateHelper.FormatYear(date);
}
