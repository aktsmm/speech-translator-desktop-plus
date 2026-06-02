using SpeechTranslatorDesktop.Models;

namespace SpeechTranslatorDesktop.Services;

public sealed class DesktopTranslationWorkerFactory : IDesktopTranslationWorkerFactory
{
    private readonly IRecordingFileService _recordingFileService;

    public DesktopTranslationWorkerFactory(IRecordingFileService recordingFileService)
    {
        _recordingFileService = recordingFileService ?? throw new ArgumentNullException(nameof(recordingFileService));
    }

    public IDesktopTranslationWorker Create(string targetLanguage, string? recordingFileName)
    {
        return Create(targetLanguage, recordingFileName, UiLanguage.Japanese);
    }

    public IDesktopTranslationWorker Create(string targetLanguage, string? recordingFileName, UiLanguage uiLanguage)
    {
        return new DesktopTranslationWorker(targetLanguage, recordingFileName, _recordingFileService, uiLanguage);
    }
}
