namespace SpeechTranslatorDesktop.Services;

public interface IRecordingFileService
{
    string RecordingsDirectory { get; }

    string? NormalizeFileName(string? fileName);

    void AppendTranslation(string? fileName, string sourceText, string translatedText);

    string OpenRecordingsFolder();

    void SetRecordingsDirectory(string directoryPath);
}
