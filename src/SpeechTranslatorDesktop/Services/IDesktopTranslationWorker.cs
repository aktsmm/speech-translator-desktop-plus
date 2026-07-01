using SpeechTranslatorDesktop.Models;
using SpeechTranslatorShared;

namespace SpeechTranslatorDesktop.Services;

public interface IDesktopTranslationWorker
{
    TranslationRecognizerWorkerBase RecognizerWorker { get; }

    TranslationRecognizerWorkerBase MicrophoneRecognizerWorker { get; }

    TranslationRecognizerWorkerBase SystemAudioRecognizerWorker { get; }

    SpeechRecognizerWorkerBase SpeechRecognizerWorker { get; }

    SpeechRecognizerWorkerBase MicrophoneSpeechRecognizerWorker { get; }

    SpeechRecognizerWorkerBase SystemAudioSpeechRecognizerWorker { get; }

    void ReportError(string message);

    void ReportRecognizing();

    void ReportTranscribedSpeech(string sourceText, AudioSourceKind audioSource = AudioSourceKind.Unspecified);

    void ReportTranslatedSpeech(string sourceText, string translatedText, AudioSourceKind audioSource = AudioSourceKind.Unspecified);

    event EventHandler<string>? MessageLogged;

    event EventHandler<WorkerStatusChangedEventArgs>? StatusChanged;

    event EventHandler<TranslationLogItem>? TranslationLogged;
}
