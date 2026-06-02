using SpeechTranslatorDesktop.Models;

namespace SpeechTranslatorDesktop.Services;

public interface IDesktopTranslationWorkerFactory
{
    IDesktopTranslationWorker Create(string targetLanguage, string? recordingFileName, UiLanguage uiLanguage);
}
