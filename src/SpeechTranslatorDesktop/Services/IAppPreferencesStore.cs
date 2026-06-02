namespace SpeechTranslatorDesktop.Services;

public interface IAppPreferencesStore
{
    Task<AppPreferences?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(AppPreferences preferences, CancellationToken cancellationToken = default);
}
