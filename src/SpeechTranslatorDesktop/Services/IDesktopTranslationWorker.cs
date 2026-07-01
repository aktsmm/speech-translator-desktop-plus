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

    event EventHandler<string>? MessageLogged;

    event EventHandler<WorkerStatusChangedEventArgs>? StatusChanged;

    event EventHandler<TranslationLogItem>? TranslationLogged;
}
