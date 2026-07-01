using SpeechTranslatorDesktop.Models;
using SpeechTranslatorDesktop.Services;
using SpeechTranslatorShared;

namespace SpeechTranslator.Desktop.Tests;

public class DesktopTranslationControllerTests
{
    [Fact]
    public async Task StopAsync_WhenSessionStopFails_KeepsControllerRunning()
    {
        var session = new FakeTranslationSession();
        session.EnqueueStopBehavior(() => throw new InvalidOperationException("stop failed"));
        session.EnqueueStopBehavior(() =>
        {
            session.Complete();
            return Task.CompletedTask;
        });
        var controller = CreateController(session);

        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.Translation, new FakeDesktopTranslationWorker());
        await FluentActions.Awaiting(() => controller.StopAsync())
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("stop failed");

        controller.IsRunning.Should().BeTrue();
        session.DisposeCallCount.Should().Be(0);

        await controller.StopAsync();

        controller.IsRunning.Should().BeFalse();
        session.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task StartAsync_AfterStopFailure_ThrowsWhileOriginalSessionIsTracked()
    {
        var session = new FakeTranslationSession();
        session.EnqueueStopBehavior(() => throw new InvalidOperationException("stop failed"));
        session.EnqueueStopBehavior(() =>
        {
            session.Complete();
            return Task.CompletedTask;
        });
        var controller = CreateController(session);

        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.Translation, new FakeDesktopTranslationWorker());
        await FluentActions.Awaiting(() => controller.StopAsync()).Should().ThrowAsync<InvalidOperationException>();

        await FluentActions.Awaiting(() => controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.Translation, new FakeDesktopTranslationWorker()))
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("Translation is already running.");

        await controller.StopAsync();
        controller.IsRunning.Should().BeFalse();
    }

    [Fact]
    public async Task StopAsync_WhenDisposeFails_KeepsControllerRunningUntilRetrySucceeds()
    {
        var session = new FakeTranslationSession();
        session.EnqueueStopBehavior(() =>
        {
            session.Complete();
            return Task.CompletedTask;
        });
        session.EnqueueStopBehavior(() => Task.CompletedTask);
        session.EnqueueDisposeBehavior(() => ValueTask.FromException(new InvalidOperationException("dispose failed")));
        session.EnqueueDisposeBehavior(() => ValueTask.CompletedTask);
        var controller = CreateController(session);

        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.Translation, new FakeDesktopTranslationWorker());
        await FluentActions.Awaiting(() => controller.StopAsync())
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("dispose failed");

        controller.IsRunning.Should().BeTrue();
        session.DisposeCallCount.Should().Be(1);

        await controller.StopAsync();

        controller.IsRunning.Should().BeFalse();
        session.DisposeCallCount.Should().Be(2);
    }

    [Fact]
    public async Task CompositeTranslationSession_WhenOneChildCompletes_StopsSiblingAndCompletesAfterAllChildren()
    {
        var first = new FakeTranslationSession();
        var second = new FakeTranslationSession();
        second.EnqueueStopBehavior(() =>
        {
            second.Complete();
            return Task.CompletedTask;
        });
        var completedCount = 0;
        await using var composite = new CompositeTranslationSession([first, second], () => completedCount++);

        first.Complete();
        await composite.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        second.StopCallCount.Should().Be(1);
        composite.IsRunning.Should().BeFalse();
        completedCount.Should().Be(1);
    }

    [Fact]
    public async Task CompositeTranslationSession_WhenCompleted_CanRaiseSingleWorkerTerminalStatus()
    {
        var first = new FakeTranslationSession();
        var second = new FakeTranslationSession();
        second.EnqueueStopBehavior(() =>
        {
            second.Complete();
            return Task.CompletedTask;
        });
        var worker = new DesktopTranslationWorker("ja-JP", null, new NoOpRecordingFileService(), audioInputSource: AudioInputSource.MicrophoneAndSystemAudio);
        var statuses = new List<WorkerStatusChangedEventArgs>();
        worker.StatusChanged += (_, e) => statuses.Add(e);
        await using var composite = new CompositeTranslationSession([first, second], worker.NotifyCombinedSessionStopped);

        first.Complete();
        await composite.Completion.WaitAsync(TimeSpan.FromSeconds(2));

        statuses.Should().ContainSingle(e => e.Status == DesktopTranslationStatus.SessionStopped);
    }

    private static DesktopTranslationController CreateController(ITranslationSession session)
    {
        return new DesktopTranslationController((speechProvider, credentials, googleSettings, sourceLanguage, targetLanguage, audioInputSource, recognitionMode, worker, cancellationToken) =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(session);
        });
    }

    private sealed class FakeTranslationSession : ITranslationSession
    {
        private readonly Queue<Func<Task>> _stopBehaviors = new();
        private readonly Queue<Func<ValueTask>> _disposeBehaviors = new();
        private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task Completion => _completion.Task;

        public bool IsRunning { get; private set; } = true;

        public int DisposeCallCount { get; private set; }

        public int StopCallCount { get; private set; }

        public void EnqueueDisposeBehavior(Func<ValueTask> behavior) => _disposeBehaviors.Enqueue(behavior);

        public void EnqueueStopBehavior(Func<Task> behavior) => _stopBehaviors.Enqueue(behavior);

        public void Complete()
        {
            IsRunning = false;
            _completion.TrySetResult();
        }

        public Task StopAsync()
        {
            StopCallCount++;
            return _stopBehaviors.Count > 0 ? _stopBehaviors.Dequeue().Invoke() : Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCallCount++;
            IsRunning = false;
            return _disposeBehaviors.Count > 0 ? _disposeBehaviors.Dequeue().Invoke() : ValueTask.CompletedTask;
        }
    }

    private sealed class NoOpTranslationRecognizerWorker : TranslationRecognizerWorkerBase
    {
        public override void OnCanceled(TranslationRecognitionCanceledEventArgs e)
        {
        }

        public override void OnRecognized(TranslationRecognitionEventArgs e)
        {
        }

        public override void OnRecognizing(TranslationRecognitionEventArgs e)
        {
        }

        public override void OnSessionStarted(SessionEventArgs e)
        {
        }

        public override void OnSessionStopped(SessionEventArgs e)
        {
        }

        public override void OnSpeechEndDetected(RecognitionEventArgs e)
        {
        }

        public override void OnSpeechStartDetected(RecognitionEventArgs e)
        {
        }
    }

    private sealed class FakeDesktopTranslationWorker : IDesktopTranslationWorker
    {
        public TranslationRecognizerWorkerBase RecognizerWorker { get; } = new NoOpTranslationRecognizerWorker();

        public TranslationRecognizerWorkerBase MicrophoneRecognizerWorker { get; } = new NoOpTranslationRecognizerWorker();

        public TranslationRecognizerWorkerBase SystemAudioRecognizerWorker { get; } = new NoOpTranslationRecognizerWorker();

        public SpeechRecognizerWorkerBase SpeechRecognizerWorker { get; } = new NoOpSpeechRecognizerWorker();

        public SpeechRecognizerWorkerBase MicrophoneSpeechRecognizerWorker { get; } = new NoOpSpeechRecognizerWorker();

        public SpeechRecognizerWorkerBase SystemAudioSpeechRecognizerWorker { get; } = new NoOpSpeechRecognizerWorker();

        public event EventHandler<string>? MessageLogged;

        public event EventHandler<WorkerStatusChangedEventArgs>? StatusChanged;

        public event EventHandler<TranslationLogItem>? TranslationLogged;

        public void ReportError(string message)
        {
        }

        public void ReportRecognizing()
        {
        }

        public void ReportTranscribedSpeech(string sourceText, AudioSourceKind audioSource = AudioSourceKind.Unspecified)
        {
        }

        public void ReportTranslatedSpeech(string sourceText, string translatedText, AudioSourceKind audioSource = AudioSourceKind.Unspecified)
        {
        }
    }

    private sealed class NoOpSpeechRecognizerWorker : SpeechRecognizerWorkerBase
    {
        public override void OnCanceled(SpeechRecognitionCanceledEventArgs e)
        {
        }

        public override void OnRecognized(SpeechRecognitionEventArgs e)
        {
        }

        public override void OnRecognizing(SpeechRecognitionEventArgs e)
        {
        }

        public override void OnSessionStarted(SessionEventArgs e)
        {
        }

        public override void OnSessionStopped(SessionEventArgs e)
        {
        }

        public override void OnSpeechEndDetected(RecognitionEventArgs e)
        {
        }

        public override void OnSpeechStartDetected(RecognitionEventArgs e)
        {
        }
    }

    private sealed class NoOpRecordingFileService : IRecordingFileService
    {
        public string RecordingsDirectory => @"C:\recordings";

        public string? NormalizeFileName(string? fileName) => fileName;

        public void AppendTranslation(string? fileName, string sourceText, string translatedText, string? speakerLabel = null)
        {
        }

        public void AppendTranscription(string? fileName, string sourceText, string? speakerLabel = null)
        {
        }

        public string OpenRecordingsFolder() => RecordingsDirectory;

        public void SetRecordingsDirectory(string directoryPath)
        {
        }
    }
}
