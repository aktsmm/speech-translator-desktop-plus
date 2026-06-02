using SpeechTranslatorShared;
using SpeechTranslatorDesktop.Models;

namespace SpeechTranslatorDesktop.Services;

public sealed class DesktopTranslationController : ITranslationController
{
    private readonly object _syncRoot = new();
    private readonly Func<SpeechCredentials, string, string, AudioInputSource, RecognitionMode, IDesktopTranslationWorker, CancellationToken, Task<ITranslationSession>> _startSessionAsync;
    private ITranslationSession? _session;
    private ITranslationSession? _sessionBeingStopped;

    public DesktopTranslationController()
        : this(StartSessionAsync)
    {
    }

    internal DesktopTranslationController(Func<SpeechCredentials, string, string, AudioInputSource, RecognitionMode, IDesktopTranslationWorker, CancellationToken, Task<ITranslationSession>> startSessionAsync)
    {
        _startSessionAsync = startSessionAsync ?? throw new ArgumentNullException(nameof(startSessionAsync));
    }

    public bool IsRunning
    {
        get
        {
            lock (_syncRoot)
            {
                return _session is not null;
            }
        }
    }

    public async Task StartAsync(SpeechCredentials credentials, string sourceLanguage, string targetLanguage, AudioInputSource audioInputSource, RecognitionMode recognitionMode, IDesktopTranslationWorker worker, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(worker);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLanguage);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetLanguage);

        lock (_syncRoot)
        {
            if (_session is not null)
            {
                throw new InvalidOperationException("Translation is already running.");
            }
        }

        cancellationToken.ThrowIfCancellationRequested();
        var session = await _startSessionAsync(credentials, sourceLanguage, targetLanguage, audioInputSource, recognitionMode, worker, cancellationToken).ConfigureAwait(false);

        lock (_syncRoot)
        {
            _session = session;
        }

        _ = ObserveCompletionAsync(session);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ITranslationSession? session;
        lock (_syncRoot)
        {
            session = _session;

            if (session is not null)
            {
                _sessionBeingStopped = session;
            }
        }

        if (session is null)
        {
            return;
        }

        try
        {
            await session.StopAsync().ConfigureAwait(false);
            await session.DisposeAsync().ConfigureAwait(false);

            lock (_syncRoot)
            {
                if (ReferenceEquals(_session, session))
                {
                    _session = null;
                }

                if (ReferenceEquals(_sessionBeingStopped, session))
                {
                    _sessionBeingStopped = null;
                }
            }
        }
        catch
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_sessionBeingStopped, session))
                {
                    _sessionBeingStopped = null;
                }
            }

            throw;
        }
    }

    private async Task ObserveCompletionAsync(ITranslationSession session)
    {
        try
        {
            await session.Completion.ConfigureAwait(false);
        }
        finally
        {
            var shouldDispose = false;
            lock (_syncRoot)
            {
                if (ReferenceEquals(_session, session) && !ReferenceEquals(_sessionBeingStopped, session))
                {
                    _session = null;
                    shouldDispose = true;
                }
            }

            if (shouldDispose)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static async Task<ITranslationSession> StartSessionAsync(SpeechCredentials credentials, string sourceLanguage, string targetLanguage, AudioInputSource audioInputSource, RecognitionMode recognitionMode, IDesktopTranslationWorker worker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (recognitionMode == RecognitionMode.TranscriptionOnly)
        {
            return await StartTranscriptionSessionAsync(credentials, sourceLanguage, audioInputSource, worker, cancellationToken).ConfigureAwait(false);
        }

        var endpointUrl = new Uri($"wss://{credentials.Region}.stt.speech.microsoft.com/speech/universal/v2");
        var translator = new Translator(endpointUrl, credentials.Key, sourceLanguage, targetLanguage);
        if (audioInputSource == AudioInputSource.Microphone)
        {
            return await translator.StartTranslationAsync(worker.RecognizerWorker).ConfigureAwait(false);
        }

        var systemAudioInput = audioInputSource == AudioInputSource.MicrophoneAndSystemAudio
            ? SystemAudioInput.StartMicrophoneAndSystemAudio()
            : SystemAudioInput.StartSystemAudio();
        try
        {
            return await translator.StartTranslationAsync(worker.RecognizerWorker, systemAudioInput.AudioConfig, systemAudioInput).ConfigureAwait(false);
        }
        catch
        {
            systemAudioInput.Dispose();
            throw;
        }
    }

    private static async Task<ITranslationSession> StartTranscriptionSessionAsync(SpeechCredentials credentials, string sourceLanguage, AudioInputSource audioInputSource, IDesktopTranslationWorker worker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var speechConfig = SpeechConfig.FromSubscription(credentials.Key, credentials.Region);
        speechConfig.SpeechRecognitionLanguage = sourceLanguage;

        if (audioInputSource == AudioInputSource.Microphone)
        {
            var microphoneAudioInput = AudioConfig.FromDefaultMicrophoneInput();
            SpeechRecognizer? microphoneRecognizer = null;
            SpeechRecognitionSession? microphoneSession = null;
            try
            {
                microphoneRecognizer = new SpeechRecognizer(speechConfig, microphoneAudioInput);
                microphoneSession = new SpeechRecognitionSession(microphoneAudioInput, microphoneRecognizer, worker.SpeechRecognizerWorker);
                await microphoneSession.StartAsync().ConfigureAwait(false);
                return microphoneSession;
            }
            catch
            {
                if (microphoneSession is not null)
                {
                    await microphoneSession.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    microphoneRecognizer?.Dispose();
                    microphoneAudioInput.Dispose();
                }

                throw;
            }
        }

        var systemAudioInput = audioInputSource == AudioInputSource.MicrophoneAndSystemAudio
            ? SystemAudioInput.StartMicrophoneAndSystemAudio()
            : SystemAudioInput.StartSystemAudio();
        SpeechRecognitionSession? session = null;
        try
        {
            var recognizer = new SpeechRecognizer(speechConfig, systemAudioInput.AudioConfig);
            session = new SpeechRecognitionSession(systemAudioInput.AudioConfig, recognizer, worker.SpeechRecognizerWorker, systemAudioInput);
            await session.StartAsync().ConfigureAwait(false);
            return session;
        }
        catch
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            else
            {
                systemAudioInput.Dispose();
            }

            throw;
        }
    }
}
