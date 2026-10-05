using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace StudioManager.App.Converters;

public sealed class SegmentHeightConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values is not { Length: >= 3 }) return 0d;

        var value = ToDouble(values[0]);
        var maximum = ToDouble(values[1]);
        var actualHeight = ToDouble(values[2]);
        var segment = parameter as string ?? "";

        if (maximum <= 0 || actualHeight <= 0)
            return segment.Contains("Margin") ? new Thickness(0) : 0d;

        var t1 = 0.50 * maximum;
        var t2 = 0.85 * maximum;

        var greenHeight = actualHeight * (Math.Min(value, t1) / maximum);
        var orangeHeight = actualHeight * (Clamp(Math.Min(value, t2) - t1, 0, t2 - t1) / maximum);
        var redHeight = actualHeight * (Clamp(value - t2, 0, maximum - t2) / maximum);

        return segment switch
        {
            "Green" => greenHeight,
            "Orange" => orangeHeight,
            "Red" => redHeight,
            "OrangeMargin" => new Thickness(0, 0, 0, greenHeight),
            "RedMargin" => new Thickness(0, 0, 0, greenHeight + orangeHeight),
            _ => 0d
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();

    private static double ToDouble(object o) => o switch
    {
        double d => d,
        float f => f,
        int i => i,
        decimal m => (double)m,
        _ => 0d
    };

    private static double Clamp(double v, double min, double max) =>
        v < min ? min : v > max ? max : v;
}
