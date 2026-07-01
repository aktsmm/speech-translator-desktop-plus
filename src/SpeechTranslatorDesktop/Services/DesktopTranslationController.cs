using SpeechTranslatorShared;
using SpeechTranslatorDesktop.Models;

namespace SpeechTranslatorDesktop.Services;

public sealed class DesktopTranslationController : ITranslationController
{
    private readonly object _syncRoot = new();
    private readonly Func<SpeechProviderKind, SpeechCredentials?, GoogleCloudServiceSettings?, string, string, AudioInputSource, RecognitionMode, IDesktopTranslationWorker, CancellationToken, Task<ITranslationSession>> _startSessionAsync;
    private ITranslationSession? _session;
    private ITranslationSession? _sessionBeingStopped;

    public DesktopTranslationController()
        : this(StartSessionAsync)
    {
    }

    internal DesktopTranslationController(Func<SpeechProviderKind, SpeechCredentials?, GoogleCloudServiceSettings?, string, string, AudioInputSource, RecognitionMode, IDesktopTranslationWorker, CancellationToken, Task<ITranslationSession>> startSessionAsync)
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

    public async Task StartAsync(SpeechProviderKind speechProvider, SpeechCredentials? credentials, GoogleCloudServiceSettings? googleSettings, string sourceLanguage, string targetLanguage, AudioInputSource audioInputSource, RecognitionMode recognitionMode, IDesktopTranslationWorker worker, CancellationToken cancellationToken = default)
    {
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
        var session = await _startSessionAsync(speechProvider, credentials, googleSettings, sourceLanguage, targetLanguage, audioInputSource, recognitionMode, worker, cancellationToken).ConfigureAwait(false);

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

    private static async Task<ITranslationSession> StartSessionAsync(SpeechProviderKind speechProvider, SpeechCredentials? credentials, GoogleCloudServiceSettings? googleSettings, string sourceLanguage, string targetLanguage, AudioInputSource audioInputSource, RecognitionMode recognitionMode, IDesktopTranslationWorker worker, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (speechProvider == SpeechProviderKind.GoogleCloud)
        {
            return await StartGoogleSessionAsync(googleSettings, sourceLanguage, targetLanguage, audioInputSource, recognitionMode, worker, cancellationToken).ConfigureAwait(false);
        }

        if (credentials is null)
        {
            throw new InvalidOperationException("Azure AI Speech credentials are required.");
        }

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

        if (audioInputSource == AudioInputSource.MicrophoneAndSystemAudio)
        {
            return await StartCombinedTranslationSessionAsync(translator, worker).ConfigureAwait(false);
        }

        var systemAudioInput = SystemAudioInput.StartSystemAudio();
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

        if (audioInputSource == AudioInputSource.MicrophoneAndSystemAudio)
        {
            return await StartCombinedTranscriptionSessionAsync(speechConfig, worker).ConfigureAwait(false);
        }

        var systemAudioInput = SystemAudioInput.StartSystemAudio();
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

    internal static async Task<ITranslationSession> StartCombinedTranslationSessionAsync(Translator translator, IDesktopTranslationWorker worker)
    {
        ArgumentNullException.ThrowIfNull(translator);
        ArgumentNullException.ThrowIfNull(worker);

        var sessions = new List<ITranslationSession>();
        try
        {
            sessions.Add(await translator.StartTranslationAsync(worker.MicrophoneRecognizerWorker).ConfigureAwait(false));

            var systemAudioInput = SystemAudioInput.StartSystemAudio();
            try
            {
                sessions.Add(await translator.StartTranslationAsync(worker.SystemAudioRecognizerWorker, systemAudioInput.AudioConfig, systemAudioInput).ConfigureAwait(false));
            }
            catch
            {
                systemAudioInput.Dispose();
                throw;
            }

            return CreateCompositeSession(sessions, worker);
        }
        catch
        {
            await DisposeStartedSessionsAsync(sessions).ConfigureAwait(false);
            throw;
        }
    }

    internal static async Task<ITranslationSession> StartCombinedTranscriptionSessionAsync(SpeechConfig speechConfig, IDesktopTranslationWorker worker)
    {
        ArgumentNullException.ThrowIfNull(speechConfig);
        ArgumentNullException.ThrowIfNull(worker);

        var sessions = new List<ITranslationSession>();
        try
        {
            var microphoneAudioInput = AudioConfig.FromDefaultMicrophoneInput();
            SpeechRecognizer? microphoneRecognizer = null;
            SpeechRecognitionSession? microphoneSession = null;
            try
            {
                microphoneRecognizer = new SpeechRecognizer(speechConfig, microphoneAudioInput);
                microphoneSession = new SpeechRecognitionSession(microphoneAudioInput, microphoneRecognizer, worker.MicrophoneSpeechRecognizerWorker);
                await microphoneSession.StartAsync().ConfigureAwait(false);
                sessions.Add(microphoneSession);
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

            var systemAudioInput = SystemAudioInput.StartSystemAudio();
            SpeechRecognitionSession? systemAudioSession = null;
            try
            {
                var systemAudioRecognizer = new SpeechRecognizer(speechConfig, systemAudioInput.AudioConfig);
                systemAudioSession = new SpeechRecognitionSession(systemAudioInput.AudioConfig, systemAudioRecognizer, worker.SystemAudioSpeechRecognizerWorker, systemAudioInput);
                await systemAudioSession.StartAsync().ConfigureAwait(false);
                sessions.Add(systemAudioSession);
            }
            catch
            {
                if (systemAudioSession is not null)
                {
                    await systemAudioSession.DisposeAsync().ConfigureAwait(false);
                }
                else
                {
                    systemAudioInput.Dispose();
                }

                throw;
            }

            return CreateCompositeSession(sessions, worker);
        }
        catch
        {
            await DisposeStartedSessionsAsync(sessions).ConfigureAwait(false);
            throw;
        }
    }

    private static CompositeTranslationSession CreateCompositeSession(IEnumerable<ITranslationSession> sessions, IDesktopTranslationWorker worker)
    {
        return new CompositeTranslationSession(sessions, () =>
        {
            if (worker is DesktopTranslationWorker desktopWorker)
            {
                desktopWorker.NotifyCombinedSessionStopped();
            }
        });
    }

    private static async Task DisposeStartedSessionsAsync(IEnumerable<ITranslationSession> sessions)
    {
        foreach (var session in sessions)
        {
            await session.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async Task<ITranslationSession> StartGoogleSessionAsync(GoogleCloudServiceSettings? settings, string sourceLanguage, string targetLanguage, AudioInputSource audioInputSource, RecognitionMode recognitionMode, IDesktopTranslationWorker worker, CancellationToken cancellationToken)
    {
        if (settings is null || string.IsNullOrWhiteSpace(settings.ProjectId))
        {
            throw new InvalidOperationException("Google Cloud project ID is required. Set it in Settings or GOOGLE_CLOUD_PROJECT.");
        }

        if (audioInputSource == AudioInputSource.MicrophoneAndSystemAudio)
        {
            var sessions = new List<ITranslationSession>();
            try
            {
                sessions.Add(await GoogleCloudTranslationSession.StartAsync(settings, sourceLanguage, targetLanguage, AudioInputSource.Microphone, recognitionMode, worker, AudioSourceKind.Microphone, cancellationToken).ConfigureAwait(false));
                sessions.Add(await GoogleCloudTranslationSession.StartAsync(settings, sourceLanguage, targetLanguage, AudioInputSource.SystemAudio, recognitionMode, worker, AudioSourceKind.SystemAudio, cancellationToken).ConfigureAwait(false));
                return CreateCompositeSession(sessions, worker);
            }
            catch
            {
                await DisposeStartedSessionsAsync(sessions).ConfigureAwait(false);
                throw;
            }
        }

        var audioSource = audioInputSource == AudioInputSource.SystemAudio
            ? AudioSourceKind.SystemAudio
            : AudioSourceKind.Unspecified;
        return await GoogleCloudTranslationSession.StartAsync(settings, sourceLanguage, targetLanguage, audioInputSource, recognitionMode, worker, audioSource, cancellationToken).ConfigureAwait(false);
    }
}
