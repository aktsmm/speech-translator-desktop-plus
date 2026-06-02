using Microsoft.CognitiveServices.Speech.Audio;
using SpeechTranslatorShared;

namespace SpeechTranslatorDesktop.Services;

public sealed class SpeechRecognitionSession : ITranslationSession
{
    private readonly AudioConfig _audioConfig;
    private readonly IDisposable? _audioInputLifetime;
    private readonly SpeechRecognizer _recognizer;
    private readonly SpeechRecognizerWorkerBase _worker;
    private readonly TaskCompletionSource _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _disposeRequested;
    private int _stopRequested;
    private bool _started;
    private bool _disposed;

    public SpeechRecognitionSession(AudioConfig audioConfig, SpeechRecognizer recognizer, SpeechRecognizerWorkerBase worker, IDisposable? audioInputLifetime = null)
    {
        _audioConfig = audioConfig ?? throw new ArgumentNullException(nameof(audioConfig));
        _audioInputLifetime = audioInputLifetime;
        _recognizer = recognizer ?? throw new ArgumentNullException(nameof(recognizer));
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));

        _recognizer.Recognizing += OnRecognizing;
        _recognizer.Recognized += OnRecognized;
        _recognizer.Canceled += OnCanceled;
        _recognizer.SpeechStartDetected += OnSpeechStartDetected;
        _recognizer.SpeechEndDetected += OnSpeechEndDetected;
        _recognizer.SessionStarted += OnSessionStarted;
        _recognizer.SessionStopped += OnSessionStopped;
    }

    public Task Completion => _completion.Task;

    public bool IsRunning { get; private set; }

    public async Task StartAsync()
    {
        ThrowIfDisposed();
        if (_started)
        {
            throw new InvalidOperationException("The speech recognition session has already been started.");
        }

        _started = true;
        await _recognizer.StartContinuousRecognitionAsync().ConfigureAwait(false);
        IsRunning = true;
    }

    public async Task StopAsync()
    {
        ThrowIfDisposed();
        if (!_started)
        {
            return;
        }

        if (Interlocked.Exchange(ref _stopRequested, 1) == 1)
        {
            await Completion.ConfigureAwait(false);
            return;
        }

        await _recognizer.StopContinuousRecognitionAsync().ConfigureAwait(false);
        Complete();
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposeRequested, 1) == 1)
        {
            return;
        }

        try
        {
            if (_started)
            {
                await StopAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            _recognizer.Recognizing -= OnRecognizing;
            _recognizer.Recognized -= OnRecognized;
            _recognizer.Canceled -= OnCanceled;
            _recognizer.SpeechStartDetected -= OnSpeechStartDetected;
            _recognizer.SpeechEndDetected -= OnSpeechEndDetected;
            _recognizer.SessionStarted -= OnSessionStarted;
            _recognizer.SessionStopped -= OnSessionStopped;
            _recognizer.Dispose();
            _audioConfig.Dispose();
            _audioInputLifetime?.Dispose();
            _disposed = true;
        }
    }

    private void OnRecognizing(object? sender, SpeechRecognitionEventArgs e) => _worker.OnRecognizing(e);

    private void OnRecognized(object? sender, SpeechRecognitionEventArgs e) => _worker.OnRecognized(e);

    private void OnCanceled(object? sender, SpeechRecognitionCanceledEventArgs e)
    {
        _worker.OnCanceled(e);
        Complete();
    }

    private void OnSpeechStartDetected(object? sender, RecognitionEventArgs e) => _worker.OnSpeechStartDetected(e);

    private void OnSpeechEndDetected(object? sender, RecognitionEventArgs e) => _worker.OnSpeechEndDetected(e);

    private void OnSessionStarted(object? sender, SessionEventArgs e) => _worker.OnSessionStarted(e);

    private void OnSessionStopped(object? sender, SessionEventArgs e)
    {
        _worker.OnSessionStopped(e);
        Complete();
    }

    private void Complete()
    {
        IsRunning = false;
        _completion.TrySetResult();
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
