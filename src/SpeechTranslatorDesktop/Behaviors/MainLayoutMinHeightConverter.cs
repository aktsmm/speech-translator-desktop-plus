using System.Globalization;
using System.Windows.Data;

namespace SpeechTranslatorDesktop.Behaviors;

public sealed class MainLayoutMinHeightConverter : IMultiValueConverter
{
    public double CardSpacing { get; set; } = 16;
    public double TranslationExpandedMinHeight { get; set; } = 240;

    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var heroHeight = ReadNonNegativeDouble(values, 0);
        var controlHeight = ReadNonNegativeDouble(values, 1);
        var statusHeight = ReadNonNegativeDouble(values, 2);
        var translationExpanded = values.Length > 3 && values[3] is bool isExpanded && isExpanded;
        var translationHeight = translationExpanded ? TranslationExpandedMinHeight : 0;

        return heroHeight + controlHeight + statusHeight + (CardSpacing * 3) + translationHeight;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotSupportedException();

    private static double ReadNonNegativeDouble(object[] values, int index)
    {
        if (index >= values.Length || values[index] is not double value || double.IsNaN(value) || double.IsInfinity(value))
        {
            return 0;
        }

        return Math.Max(0, value);
    }
}
