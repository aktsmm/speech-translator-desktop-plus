namespace SpeechTranslatorShared;

public class Translator
{
    private readonly SpeechTranslationConfig _speechTranslationConfig;

    public Translator(Uri endpointUrl, string subscriptionKey, string recognitionLanguage = "en-US", string targetLanguage = "ja-JP")
    {
        if (endpointUrl is null)
        {
            throw new ArgumentNullException(nameof(endpointUrl));
        }

        if (string.IsNullOrWhiteSpace(subscriptionKey))
        {
            throw new ArgumentException($"'{nameof(subscriptionKey)}' を NULL または空にすることはできません。", nameof(subscriptionKey));
        }

        if (string.IsNullOrWhiteSpace(recognitionLanguage))
        {
            throw new ArgumentException($"'{nameof(recognitionLanguage)}' を NULL または空にすることはできません。", nameof(recognitionLanguage));
        }

        if (string.IsNullOrWhiteSpace(targetLanguage))
        {
            throw new ArgumentException($"'{nameof(targetLanguage)}' を NULL または空にすることはできません。", nameof(targetLanguage));
        }

        _speechTranslationConfig = SpeechTranslationConfig.FromEndpoint(endpointUrl, subscriptionKey);
        _speechTranslationConfig.SpeechRecognitionLanguage = recognitionLanguage;
        _speechTranslationConfig.AddTargetLanguage(targetLanguage);
        _speechTranslationConfig.SetProperty(PropertyId.SpeechServiceConnection_TranslationVoice, "en-US-JennyNeural");
    }

    public async Task<ITranslationSession> StartTranslationAsync(TranslationRecognizerWorkerBase worker)
    {
        var audioInput = AudioConfig.FromDefaultMicrophoneInput();
        return await StartTranslationAsync(worker, audioInput).ConfigureAwait(false);
    }

    public async Task<ITranslationSession> StartTranslationAsync(TranslationRecognizerWorkerBase worker, AudioConfig audioInput, IDisposable? audioInputLifetime = null)
    {
        if (worker is null)
        {
            throw new ArgumentNullException(nameof(worker));
        }

        if (audioInput is null)
        {
            audioInputLifetime?.Dispose();
            throw new ArgumentNullException(nameof(audioInput));
        }

        var autoDetectSourceLanguageConfig = AutoDetectSourceLanguageConfig.FromLanguages([_speechTranslationConfig.SpeechRecognitionLanguage]);
        var recognizer = new TranslationRecognizer(_speechTranslationConfig, autoDetectSourceLanguageConfig, audioInput);
        var session = new TranslationSession(audioInput, recognizer, worker, audioInputLifetime);

        await session.StartAsync().ConfigureAwait(false);
        return session;
    }

    public async Task MultiLingualTranslation(TranslationRecognizerWorkerBase worker)
    {
        await using var session = await StartTranslationAsync(worker).ConfigureAwait(false);
        await session.Completion.ConfigureAwait(false);
    }
}
