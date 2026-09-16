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
    public async Task Completion_NotifiesAfterCleanupAndClearsRunningState()
    {
        var session = new FakeTranslationSession();
        var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.EnqueueDisposeBehavior(() =>
        {
            disposeStarted.SetResult();
            return new ValueTask(disposeGate.Task);
        });
        var controller = CreateController(session);
        var ended = new TaskCompletionSource<SessionEndedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.SessionEnded += (_, e) => ended.TrySetResult(e);
        var worker = new FakeDesktopTranslationWorker();
        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.TranscriptionOnly, worker);

        session.Complete();
        await disposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        controller.IsRunning.Should().BeTrue();
        ended.Task.IsCompleted.Should().BeFalse();
        disposeGate.SetResult();
        var result = await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Worker.Should().BeSameAs(worker);
        result.Error.Should().BeNull();
        controller.IsRunning.Should().BeFalse();
        session.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Completion_AfterAutomaticCleanup_CanStartAndStopNewSession()
    {
        var first = new FakeTranslationSession();
        var second = new FakeTranslationSession();
        second.EnqueueStopBehavior(() =>
        {
            second.Complete();
            return Task.CompletedTask;
        });
        var sessions = new Queue<ITranslationSession>([first, second]);
        var controller = new DesktopTranslationController((_, _, _, _, _, _, _, _, _) =>
            Task.FromResult(sessions.Dequeue()));
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.SessionEnded += (_, _) => ended.TrySetResult();

        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.TranscriptionOnly, new FakeDesktopTranslationWorker());
        first.Complete();
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.TranscriptionOnly, new FakeDesktopTranslationWorker());
        controller.IsRunning.Should().BeTrue();

        await controller.StopAsync();

        controller.IsRunning.Should().BeFalse();
        first.DisposeCallCount.Should().Be(1);
        second.StopCallCount.Should().Be(1);
        second.DisposeCallCount.Should().Be(1);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StopAsync_DuringAutomaticCleanup_WaitsWithoutDisposingTwice(bool cleanupFails)
    {
        var session = new FakeTranslationSession();
        var disposeStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var disposeGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        session.EnqueueDisposeBehavior(() =>
        {
            disposeStarted.SetResult();
            return new ValueTask(disposeGate.Task);
        });
        var controller = CreateController(session);
        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.TranscriptionOnly, new FakeDesktopTranslationWorker());

        session.Complete();
        await disposeStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = controller.StopAsync();
        stop.IsCompleted.Should().BeFalse();
        if (cleanupFails)
        {
            disposeGate.SetException(new InvalidOperationException("dispose failed"));
            await FluentActions.Awaiting(() => stop.WaitAsync(TimeSpan.FromSeconds(5)))
                .Should().ThrowAsync<InvalidOperationException>()
                .WithMessage("*dispose failed*");
        }
        else
        {
            disposeGate.SetResult();
            await stop.WaitAsync(TimeSpan.FromSeconds(5));
        }

        controller.IsRunning.Should().Be(cleanupFails);
        session.StopCallCount.Should().Be(0);
        session.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Completion_WhenCleanupFails_ReportsErrorAndRetainsSessionForRetry()
    {
        var session = new FakeTranslationSession();
        var error = new InvalidOperationException("cleanup failed");
        session.EnqueueDisposeBehavior(() => ValueTask.FromException(error));
        var controller = CreateController(session);
        var ended = new TaskCompletionSource<SessionEndedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.SessionEnded += (_, e) => ended.TrySetResult(e);
        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.TranscriptionOnly, new FakeDesktopTranslationWorker());

        session.Complete();
        var result = await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));
        result.Error.Should().BeSameAs(error);
        controller.IsRunning.Should().BeTrue();

        await controller.StopAsync();
        controller.IsRunning.Should().BeFalse();
        session.DisposeCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Completion_WhenFaulted_StillCleansUpAndReportsFailure()
    {
        var session = new FakeTranslationSession();
        var error = new InvalidOperationException("recognition failed");
        var controller = CreateController(session);
        var ended = new TaskCompletionSource<SessionEndedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.SessionEnded += (_, e) => ended.TrySetResult(e);
        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.TranscriptionOnly, new FakeDesktopTranslationWorker());

        session.Fail(error);
        var result = await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));

        result.Error.Should().BeSameAs(error);
        controller.IsRunning.Should().BeFalse();
        session.DisposeCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Completion_WhenAlreadyCompleteDuringStart_StillNotifies()
    {
        var session = new FakeTranslationSession();
        session.Complete();
        var controller = CreateController(session);
        var ended = new TaskCompletionSource<SessionEndedEventArgs>(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.SessionEnded += (_, e) => ended.TrySetResult(e);

        await controller.StartAsync(SpeechProviderKind.AzureAiSpeech, new SpeechCredentials("japaneast", "test-key"), null, "en-US", "ja-JP", AudioInputSource.Microphone, RecognitionMode.TranscriptionOnly, new FakeDesktopTranslationWorker());
        await ended.Task.WaitAsync(TimeSpan.FromSeconds(5));

        controller.IsRunning.Should().BeFalse();
        session.DisposeCallCount.Should().Be(1);
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

        public void Fail(Exception error)
        {
            IsRunning = false;
            _completion.TrySetException(error);
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

        public string OpenRecordingsFolder(string? directoryPath = null) => directoryPath ?? RecordingsDirectory;

        public void SetRecordingsDirectory(string directoryPath)
        {
        }
    }
}
