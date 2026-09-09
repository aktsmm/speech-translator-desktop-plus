using SpeechTranslatorDesktop.Behaviors;
using System.Globalization;

namespace SpeechTranslator.Desktop.Tests;

public class MainLayoutMinHeightConverterTests
{
    [Fact]
    public void Convert_WhenTranslationExpanded_AddsExpandedMinimumHeight()
    {
        var converter = new MainLayoutMinHeightConverter();

        var result = converter.Convert([120d, 280d, 90d, true], typeof(double), null!, CultureInfo.InvariantCulture);

        result.Should().Be(120d + 280d + 90d + 48d + 240d);
    }

    [Fact]
    public void Convert_WhenTranslationCollapsed_DoesNotAddExpandedMinimumHeight()
    {
        var converter = new MainLayoutMinHeightConverter();

        var result = converter.Convert([120d, 280d, 90d, false], typeof(double), null!, CultureInfo.InvariantCulture);

        result.Should().Be(120d + 280d + 90d + 48d);
    }
}
