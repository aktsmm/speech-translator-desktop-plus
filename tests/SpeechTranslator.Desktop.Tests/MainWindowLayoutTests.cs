using System.Xml.Linq;
using FluentAssertions;

namespace SpeechTranslator.Desktop.Tests;

public class MainWindowLayoutTests
{
    [Fact]
    public void RightOperationArea_UsesCompactWrapLayout_WithoutFixedWrapItemHeight()
    {
        var document = XDocument.Load(GetMainWindowXamlPath());
        var ns = document.Root!.Name.Namespace;

        var rightOperationGrid = document
            .Descendants(ns + "Grid")
            .Single(grid =>
                (string?)grid.Attribute("Grid.Row") == "1" &&
                (string?)grid.Attribute("Grid.Column") == "2" &&
                (string?)grid.Attribute("Grid.ColumnSpan") == "2");

        var rowDefinitions = rightOperationGrid
            .Element(ns + "Grid.RowDefinitions")!
            .Elements(ns + "RowDefinition")
            .ToList();
        rowDefinitions.Should().HaveCount(4);

        var actionWrapPanel = rightOperationGrid
            .Elements(ns + "WrapPanel")
            .Single(panel => (string?)panel.Attribute("Grid.Row") == "1");
        actionWrapPanel.Attribute("HorizontalAlignment")?.Value.Should().Be("Right");
        actionWrapPanel.Attribute("ItemHeight").Should().BeNull();

        rightOperationGrid
            .Descendants(ns + "Border")
            .Attributes("MinWidth")
            .Any(attribute => attribute.Value == "170")
            .Should()
            .BeFalse();
        rightOperationGrid
            .Descendants(ns + "Border")
            .Attributes("MaxWidth")
            .Any(attribute => attribute.Value == "320")
            .Should()
            .BeFalse();

        rightOperationGrid
            .Elements(ns + "TextBlock")
            .Single(textBlock =>
                (string?)textBlock.Attribute("Grid.Row") == "2" &&
                ((string?)textBlock.Attribute("Text"))?.Contains("StatusDetailMessage") == true &&
                ((string?)textBlock.Attribute("Visibility"))?.Contains("StatusDetailVisibility") == true);

        rightOperationGrid
            .Elements(ns + "Border")
            .Single(border =>
                (string?)border.Attribute("Grid.Row") == "3" &&
                ((string?)border.Attribute("Visibility"))?.Contains("UpdateBannerVisibility") == true);
    }

    [Fact]
    public void MainLayout_UsesRootScrollViewer_AndTranslationLogMinimumHeight()
    {
        var document = XDocument.Load(GetMainWindowXamlPath());
        var ns = document.Root!.Name.Namespace;

        var rootScrollViewer = document.Root!.Elements(ns + "ScrollViewer").Single();
        rootScrollViewer.Attribute("VerticalScrollBarVisibility")?.Value.Should().Be("Auto");
        rootScrollViewer.Attribute("HorizontalScrollBarVisibility")?.Value.Should().Be("Disabled");
        rootScrollViewer.Attribute("Padding")?.Value.Should().Be("20");

        var rootGrid = rootScrollViewer.Elements(ns + "Grid").Single();
        rootGrid.Attribute("Height")?.Value.Should().Contain("ViewportHeight");

        var minHeightBinding = rootGrid.Element(ns + "Grid.MinHeight")?.Element(ns + "MultiBinding");
        minHeightBinding.Should().NotBeNull();
        minHeightBinding!.Attribute("Converter")?.Value.Should().Contain("MainLayoutMinHeightConverter");
        minHeightBinding
            .Elements(ns + "Binding")
            .Select(binding => ((string?)binding.Attribute("ElementName"), (string?)binding.Attribute("Path")))
            .Should()
            .Contain(
            [
                ("HeroCard", "ActualHeight"),
                ("ControlCard", "ActualHeight"),
                ("StatusCard", "ActualHeight"),
                ("TranslationLogExpander", "IsExpanded")
            ]);

        var translationLogCard = rootGrid
            .Elements(ns + "Border")
            .Single(border => (string?)border.Attribute("Grid.Row") == "2");
        translationLogCard.Attribute("MinHeight").Should().BeNull();
    }

    private static string GetMainWindowXamlPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "speech-translator.sln")))
        {
            directory = directory.Parent;
        }

        directory.Should().NotBeNull("the test should run under the repository tree");
        return Path.Combine(directory!.FullName, "src", "SpeechTranslatorDesktop", "MainWindow.xaml");
    }
}
