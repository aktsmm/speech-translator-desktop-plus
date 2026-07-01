using SpeechTranslatorShared;
using SpeechTranslatorDesktop.Models;

namespace SpeechTranslatorDesktop.Services;

public interface ITranslationController
{
    bool IsRunning { get; }

    Task StartAsync(SpeechProviderKind speechProvider, SpeechCredentials? credentials, GoogleCloudServiceSettings? googleSettings, string sourceLanguage, string targetLanguage, AudioInputSource audioInputSource, RecognitionMode recognitionMode, IDesktopTranslationWorker worker, CancellationToken cancellationToken = default);

    Task StopAsync(CancellationToken cancellationToken = default);
}
