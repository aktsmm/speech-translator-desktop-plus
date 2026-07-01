using System.Text;
using SpeechTranslatorDesktop.Services;

namespace SpeechTranslator.Desktop.Tests;

public class RecordingFileServiceTests : IDisposable
{
    private readonly string _rootDirectory = Path.Combine(AppContext.BaseDirectory, "recording-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void AppendTranslation_FileNameProvided_WritesUtf8Recording()
    {
        var service = new RecordingFileService(_rootDirectory);

        service.AppendTranslation("session-01", "hello", "こんにちは");

        var filePath = Path.Combine(_rootDirectory, "recordings", "session-01.txt");
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath, Encoding.UTF8).Should().Contain("hello").And.Contain("こんにちは");
    }

    [Fact]
    public void AppendTranslation_WhenSpeakerLabelProvided_WritesLabeledSourceLine()
    {
        var service = new RecordingFileService(_rootDirectory);

        service.AppendTranslation("session-01", "hello", "こんにちは", "自分");

        var filePath = Path.Combine(_rootDirectory, "recordings", "session-01.txt");
        File.ReadAllText(filePath, Encoding.UTF8).Should().Contain("自分: hello");
    }

    [Fact]
    public void AppendTranslation_EmptyFileName_DoesNotCreateRecording()
    {
        var service = new RecordingFileService(_rootDirectory);

        service.AppendTranslation(string.Empty, "hello", "こんにちは");

        Directory.Exists(Path.Combine(_rootDirectory, "recordings")).Should().BeFalse();
    }

    [Fact]
    public void AppendTranscription_FileNameProvided_WritesUtf8Recording()
    {
        var service = new RecordingFileService(_rootDirectory);

        service.AppendTranscription("session-01", "hello transcript");

        var filePath = Path.Combine(_rootDirectory, "recordings", "session-01.txt");
        File.Exists(filePath).Should().BeTrue();
        File.ReadAllText(filePath, Encoding.UTF8).Should().Contain("hello transcript");
    }

    [Fact]
    public void AppendTranscription_WhenSpeakerLabelProvided_WritesLabeledLine()
    {
        var service = new RecordingFileService(_rootDirectory);

        service.AppendTranscription("session-01", "hello transcript", "自分");

        var filePath = Path.Combine(_rootDirectory, "recordings", "session-01.txt");
        File.ReadAllText(filePath, Encoding.UTF8).Should().Contain("自分: hello transcript");
    }

    [Fact]
    public void AppendTranscription_WhenCalledConcurrently_SerializesWrites()
    {
        var service = new RecordingFileService(_rootDirectory);

        Parallel.For(0, 20, index => service.AppendTranscription("session-01", $"line-{index}", index % 2 == 0 ? "自分" : null));

        var filePath = Path.Combine(_rootDirectory, "recordings", "session-01.txt");
        var text = File.ReadAllText(filePath, Encoding.UTF8);
        text.Should().Contain("line-0").And.Contain("line-19");
    }

    [Theory]
    [InlineData(@"C:\escape")]
    [InlineData("..\\escape")]
    [InlineData("../escape")]
    [InlineData("nested/file")]
    [InlineData("nested\\file")]
    [InlineData("session.txt")]
    [InlineData("session 01")]
    [InlineData("..")]
    public void AppendTranscription_InvalidFileName_ThrowsArgumentException(string fileName)
    {
        var service = new RecordingFileService(_rootDirectory);

        var act = () => service.AppendTranscription(fileName, "hello");

        act.Should().Throw<ArgumentException>();
        Directory.Exists(Path.Combine(_rootDirectory, "recordings")).Should().BeFalse();
    }

    [Theory]
    [InlineData(@"C:\escape")]
    [InlineData("..\\escape")]
    [InlineData("../escape")]
    [InlineData("nested/file")]
    [InlineData("nested\\file")]
    [InlineData("session.txt")]
    [InlineData("session 01")]
    [InlineData("..")]
    public void AppendTranslation_InvalidFileName_ThrowsArgumentException(string fileName)
    {
        var service = new RecordingFileService(_rootDirectory);

        var act = () => service.AppendTranslation(fileName, "hello", "こんにちは");

        act.Should().Throw<ArgumentException>();
        Directory.Exists(Path.Combine(_rootDirectory, "recordings")).Should().BeFalse();
    }

    [Fact]
    public void NormalizeFileName_ValidFileName_ReturnsTrimmedValue()
    {
        var service = new RecordingFileService(_rootDirectory);

        var normalized = service.NormalizeFileName(" session-01 ");

        normalized.Should().Be("session-01");
    }

    [Fact]
    public void NormalizeFileName_WhitespaceOnly_ReturnsNull()
    {
        var service = new RecordingFileService(_rootDirectory);

        var normalized = service.NormalizeFileName("   ");

        normalized.Should().BeNull();
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }
}
