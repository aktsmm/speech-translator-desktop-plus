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
    [InlineData(false)]
    [InlineData(true)]
    public void AppendEntry_WritesFirstTimestampAndExactlyFiveMinuteBoundary(bool translate)
    {
        var clock = new ManualTimeProvider();
        var service = new RecordingFileService(_rootDirectory, clock);
        void Append(string text)
        {
            if (translate)
            {
                service.AppendTranslation("timed", text, $"translation-{text}", "自分");
            }
            else
            {
                service.AppendTranscription("timed", text, "自分");
            }
        }

        Append("first");
        clock.Advance(TimeSpan.FromMinutes(5) - TimeSpan.FromTicks(1));
        Append("before");
        clock.Advance(TimeSpan.FromTicks(1));
        Append("boundary");
        clock.Advance(TimeSpan.FromMinutes(5));
        Append("next");

        var lines = File.ReadAllLines(Path.Combine(service.RecordingsDirectory, "timed.txt"));
        lines.Where(line => line.StartsWith('[')).Should().Equal(
            "[2026-09-16 23:55:00 +09:00]",
            "[2026-09-17 00:00:00 +09:00]",
            "[2026-09-17 00:05:00 +09:00]");
        Array.IndexOf(lines, "[2026-09-17 00:00:00 +09:00]").Should().Be(Array.IndexOf(lines, "自分: boundary") - 1);
        if (translate)
        {
            lines[Array.IndexOf(lines, "自分: boundary") + 1].Should().Be("translation-boundary");
        }
    }

    [Fact]
    public void AppendEntry_AfterLongSilence_WritesOnlyResumeTimestamp()
    {
        var clock = new ManualTimeProvider();
        var service = new RecordingFileService(_rootDirectory, clock);
        service.AppendTranscription("timed", "before silence");
        var path = Path.Combine(service.RecordingsDirectory, "timed.txt");
        var previousText = File.ReadAllText(path);

        clock.Advance(TimeSpan.FromHours(2));
        File.ReadAllText(path).Should().Be(previousText);
        service.AppendTranscription("timed", "after silence");

        File.ReadAllLines(path).Where(line => line.StartsWith('[')).Should().Equal(
            "[2026-09-16 23:55:00 +09:00]",
            "[2026-09-17 01:55:00 +09:00]");
    }

    [Fact]
    public void AppendEntry_UsesIndependentIntervalsForEachFileAndDirectory()
    {
        var clock = new ManualTimeProvider();
        var service = new RecordingFileService(_rootDirectory, clock);
        var originalDirectory = service.RecordingsDirectory;
        service.AppendTranscription("first", "one");
        clock.Advance(TimeSpan.FromMinutes(2));
        service.AppendTranslation("second", "two", "translated");
        clock.Advance(TimeSpan.FromMinutes(3));
        service.AppendTranscription("first", "three");
        service.AppendTranslation("second", "four", "translated");
        service.SetRecordingsDirectory(Path.Combine(_rootDirectory, "different"));
        service.AppendTranscription("first", "five");

        File.ReadAllLines(Path.Combine(originalDirectory, "first.txt")).Count(line => line.StartsWith('[')).Should().Be(2);
        File.ReadAllLines(Path.Combine(originalDirectory, "second.txt")).Count(line => line.StartsWith('[')).Should().Be(1);
        File.ReadAllLines(Path.Combine(service.RecordingsDirectory, "first.txt"))[0].Should().Be("[2026-09-17 00:00:00 +09:00]");
    }

    [Fact]
    public void AppendEntry_ConcurrentSources_ShareOneTimestampAndKeepAllEntries()
    {
        var service = new RecordingFileService(_rootDirectory, new ManualTimeProvider());
        Parallel.For(0, 50, index =>
        {
            if (index % 2 == 0)
            {
                service.AppendTranslation("timed", $"source-{index}", $"translated-{index}", "自分");
            }
            else
            {
                service.AppendTranscription("timed", $"source-{index}");
            }
        });

        var lines = File.ReadAllLines(Path.Combine(service.RecordingsDirectory, "timed.txt"));
        lines.Count(line => line.StartsWith('[')).Should().Be(1);
        for (var index = 0; index < 50; index++)
        {
            var source = index % 2 == 0 ? $"自分: source-{index}" : $"source-{index}";
            lines.Count(line => line == source).Should().Be(1);
            if (index % 2 == 0)
            {
                lines[Array.IndexOf(lines, source) + 1].Should().Be($"translated-{index}");
            }
        }
    }

    [Fact]
    public void AppendEntry_WhenWriteFails_DoesNotConsumeTimestampInterval()
    {
        var clock = new ManualTimeProvider();
        var service = new RecordingFileService(_rootDirectory, clock);
        service.AppendTranscription("timed", "first");
        clock.Advance(TimeSpan.FromMinutes(5));
        var path = Path.Combine(service.RecordingsDirectory, "timed.txt");
        using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var append = () => service.AppendTranscription("timed", "blocked");
            append.Should().Throw<IOException>();
        }

        service.AppendTranscription("timed", "retry");

        var text = File.ReadAllText(path);
        text.Should().Contain("[2026-09-17 00:00:00 +09:00]").And.Contain("retry").And.NotContain("blocked");
    }

    [Fact]
    public void AppendEntry_WhenWallClockMovesBack_StillAddsMarkerAfterFiveElapsedMinutes()
    {
        var clock = new ManualTimeProvider();
        var service = new RecordingFileService(_rootDirectory, clock);
        service.AppendTranscription("timed", "first");
        clock.Advance(TimeSpan.FromMinutes(5));
        clock.AdjustWallClock(TimeSpan.FromHours(-1));

        service.AppendTranscription("timed", "second");

        File.ReadAllLines(Path.Combine(service.RecordingsDirectory, "timed.txt"))
            .Where(line => line.StartsWith('[')).Should().Equal(
                "[2026-09-16 23:55:00 +09:00]",
                "[2026-09-16 23:00:00 +09:00]");
    }

    [Fact]
    public void AppendEntry_WhenFileIsRecreated_WritesNewHeader()
    {
        var service = new RecordingFileService(_rootDirectory, new ManualTimeProvider());
        service.AppendTranscription("timed", "first");
        var path = Path.Combine(service.RecordingsDirectory, "timed.txt");
        File.Delete(path);

        service.AppendTranscription("timed", "second");

        File.ReadAllLines(path)[0].Should().Be("[2026-09-16 23:55:00 +09:00]");
    }

    [Theory]
    [InlineData(@"C:\escape")]
    [InlineData("..\\escape")]
    [InlineData("../escape")]
    [InlineData("nested/file")]
    [InlineData("nested\\file")]
    [InlineData("session.txt")]
    [InlineData("session🙂01")]
    [InlineData("..")]
    [InlineData("CON")]
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
    [InlineData("session🙂01")]
    [InlineData("..")]
    [InlineData("NUL")]
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

        var normalized = service.NormalizeFileName(" 会議メモ 01_A-2 ");

        normalized.Should().Be("会議メモ 01_A-2");
    }

    [Fact]
    public void NormalizeFileName_TrailingWhitespace_IsTrimmedForCompatibility()
    {
        var service = new RecordingFileService(_rootDirectory);

        var normalized = service.NormalizeFileName("session-01   ");

        normalized.Should().Be("session-01");
    }

    [Fact]
    public void NormalizeFileName_TrailingDot_ThrowsArgumentException()
    {
        var service = new RecordingFileService(_rootDirectory);

        var act = () => service.NormalizeFileName("session.");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void NormalizeFileName_WhitespaceOnly_ReturnsNull()
    {
        var service = new RecordingFileService(_rootDirectory);

        var normalized = service.NormalizeFileName("   ");

        normalized.Should().BeNull();
    }

    [Fact]
    public void NormalizeFileName_CombiningCharacter_NormalizesToFormC()
    {
        var service = new RecordingFileService(_rootDirectory);

        var normalized = service.NormalizeFileName("Cafe\u0301");

        normalized.Should().Be("Café");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootDirectory))
        {
            Directory.Delete(_rootDirectory, recursive: true);
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 9, 16, 14, 55, 0, TimeSpan.Zero);
        private long _timestamp;

        public override TimeZoneInfo LocalTimeZone { get; } =
            TimeZoneInfo.CreateCustomTimeZone("Test JST", TimeSpan.FromHours(9), "Test JST", "Test JST");
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public override long GetTimestamp() => _timestamp;

        public void AdjustWallClock(TimeSpan adjustment) => _utcNow += adjustment;

        public void Advance(TimeSpan elapsed)
        {
            _utcNow += elapsed;
            _timestamp += elapsed.Ticks;
        }
    }
}
