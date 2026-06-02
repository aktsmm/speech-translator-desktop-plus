namespace SpeechTranslatorDesktop.Services;

public interface IRecordingFolderSettingsStore
{
    Task<string?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(string directoryPath, CancellationToken cancellationToken = default);
}
