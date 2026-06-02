using SpeechTranslatorDesktop.Models;
using SpeechTranslatorShared;

namespace SpeechTranslatorDesktop.Services;

public interface IDesktopTranslationWorker
{
    TranslationRecognizerWorkerBase RecognizerWorker { get; }

    SpeechRecognizerWorkerBase SpeechRecognizerWorker { get; }

    event EventHandler<string>? MessageLogged;

    event EventHandler<WorkerStatusChangedEventArgs>? StatusChanged;

    event EventHandler<TranslationLogItem>? TranslationLogged;
}
