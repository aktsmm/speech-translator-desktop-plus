using SpeechTranslatorDesktop.Models;
using SpeechTranslatorDesktop.Services;

namespace SpeechTranslator.Desktop.Tests;

public class DesktopTranslationWorkerTests
{
    [Fact]
    public void HandleTranslatedSpeech_WhenRecordingWriteFails_RaisesErrorWithoutThrowing()
    {
        var recordingFileService = new ThrowingRecordingFileService(new IOException("disk full"));
        var worker = new DesktopTranslationWorker("ja-JP", "session-01", recordingFileService);
        var statuses = new List<WorkerStatusChangedEventArgs>();
        var translations = new List<TranslationLogItem>();

        worker.StatusChanged += (_, e) => statuses.Add(e);
        worker.TranslationLogged += (_, e) => translations.Add(e);

        var act = () => worker.HandleTranslatedSpeech("hello", "こんにちは");

        act.Should().NotThrow();
        translations.Should().ContainSingle();
        statuses.Should().ContainSingle(e =>
            e.Status == DesktopTranslationStatus.Error &&
            e.Message.Contains("記録ファイルの保存に失敗しました") &&
            e.Message.Contains("disk full"));
        statuses.Should().NotContain(e => e.Status == DesktopTranslationStatus.TranslatedSpeech);
    }

    [Fact]
    public void HandleTranslatedSpeech_WhenSourceAndTranslationAreEmpty_DoesNotLogOrSave()
    {
        var recordingFileService = new RecordingSpyFileService();
        var worker = new DesktopTranslationWorker("ja-JP", "session-01", recordingFileService);
        var statuses = new List<WorkerStatusChangedEventArgs>();
        var translations = new List<TranslationLogItem>();
        var messages = new List<string>();

        worker.StatusChanged += (_, e) => statuses.Add(e);
        worker.TranslationLogged += (_, e) => translations.Add(e);
        worker.MessageLogged += (_, e) => messages.Add(e);

        worker.HandleTranslatedSpeech("   ", "");

        statuses.Should().BeEmpty();
        translations.Should().BeEmpty();
        messages.Should().BeEmpty();
        recordingFileService.AppendCallCount.Should().Be(0);
    }

    [Fact]
    public void HandleTranslatedSpeech_WhenTranslationIsEmpty_DoesNotLogOrSave()
    {
        var recordingFileService = new RecordingSpyFileService();
        var worker = new DesktopTranslationWorker("ja-JP", "session-01", recordingFileService);
        var translations = new List<TranslationLogItem>();

        worker.TranslationLogged += (_, e) => translations.Add(e);

        worker.HandleTranslatedSpeech("hello", " ");

        translations.Should().BeEmpty();
        recordingFileService.AppendCallCount.Should().Be(0);
    }

    [Fact]
    public void HandleTranslatedSpeech_TrimsTextBeforeLoggingAndSaving()
    {
        var recordingFileService = new RecordingSpyFileService();
        var worker = new DesktopTranslationWorker("ja-JP", "session-01", recordingFileService);
        var translations = new List<TranslationLogItem>();

        worker.TranslationLogged += (_, e) => translations.Add(e);

        worker.HandleTranslatedSpeech("  hello  ", "  こんにちは  ");

        translations.Should().ContainSingle().Which.Should().BeEquivalentTo(new TranslationLogItem("hello", "こんにちは"));
        recordingFileService.LastSourceText.Should().Be("hello");
        recordingFileService.LastTranslatedText.Should().Be("こんにちは");
    }

    [Fact]
    public void TranslationLogItem_AutomationText_IncludesTranslationWhenPresent()
    {
        var item = new TranslationLogItem("hello", "こんにちは");

        item.AutomationText.Should().Be("hello こんにちは");
    }

    [Fact]
    public void HandleTranslatedSpeech_WhenEnglishUiAndRecordingWriteFails_RaisesEnglishError()
    {
        var recordingFileService = new ThrowingRecordingFileService(new IOException("disk full"));
        var worker = new DesktopTranslationWorker("ja-JP", "session-01", recordingFileService, UiLanguage.English);
        var statuses = new List<WorkerStatusChangedEventArgs>();

        worker.StatusChanged += (_, e) => statuses.Add(e);

        worker.HandleTranslatedSpeech("hello", "こんにちは");

        statuses.Should().ContainSingle(e =>
            e.Status == DesktopTranslationStatus.Error &&
            e.Message.Contains("Failed to save recording file") &&
            e.Message.Contains("disk full"));
    }

    [Fact]
    public void HandleTranscribedSpeech_TrimsTextBeforeLoggingAndSaving()
    {
        var recordingFileService = new RecordingSpyFileService();
        var worker = new DesktopTranslationWorker("ja-JP", "session-01", recordingFileService, recognitionMode: RecognitionMode.TranscriptionOnly);
        var translations = new List<TranslationLogItem>();

        worker.TranslationLogged += (_, e) => translations.Add(e);

        worker.HandleTranscribedSpeech("  hello transcript  ");

        translations.Should().ContainSingle().Which.Should().BeEquivalentTo(new TranslationLogItem("hello transcript", string.Empty));
        translations[0].AutomationText.Should().Be("hello transcript");
        recordingFileService.LastTranscriptionText.Should().Be("hello transcript");
        recordingFileService.AppendTranscriptionCallCount.Should().Be(1);
    }

    [Fact]
    public void HandleTranscribedSpeech_WhenSourceIsEmpty_DoesNotLogOrSave()
    {
        var recordingFileService = new RecordingSpyFileService();
        var worker = new DesktopTranslationWorker("ja-JP", "session-01", recordingFileService, recognitionMode: RecognitionMode.TranscriptionOnly);
        var translations = new List<TranslationLogItem>();

        worker.TranslationLogged += (_, e) => translations.Add(e);

        worker.HandleTranscribedSpeech(" ");

        translations.Should().BeEmpty();
        recordingFileService.AppendTranscriptionCallCount.Should().Be(0);
    }

    private sealed class ThrowingRecordingFileService : IRecordingFileService
    {
        private readonly Exception _exception;

        public ThrowingRecordingFileService(Exception exception)
        {
            _exception = exception;
        }

        public string? NormalizeFileName(string? fileName) => fileName;

        public void AppendTranslation(string? fileName, string sourceText, string translatedText)
        {
            throw _exception;
        }

        public void AppendTranscription(string? fileName, string sourceText)
        {
            throw _exception;
        }

        public string RecordingsDirectory => @"C:\recordings";

        public string OpenRecordingsFolder() => throw _exception;

        public void SetRecordingsDirectory(string directoryPath)
        {
        }
    }

    private sealed class RecordingSpyFileService : IRecordingFileService
    {
        public string RecordingsDirectory => @"C:\recordings";
        public int AppendCallCount { get; private set; }
        public int AppendTranscriptionCallCount { get; private set; }
        public string? LastSourceText { get; private set; }
        public string? LastTranslatedText { get; private set; }
        public string? LastTranscriptionText { get; private set; }

        public string? NormalizeFileName(string? fileName) => fileName;

        public void AppendTranslation(string? fileName, string sourceText, string translatedText)
        {
            AppendCallCount++;
            LastSourceText = sourceText;
            LastTranslatedText = translatedText;
        }

        public void AppendTranscription(string? fileName, string sourceText)
        {
            AppendTranscriptionCallCount++;
            LastTranscriptionText = sourceText;
        }

        public string OpenRecordingsFolder() => RecordingsDirectory;

        public void SetRecordingsDirectory(string directoryPath)
        {
        }
    }
}
