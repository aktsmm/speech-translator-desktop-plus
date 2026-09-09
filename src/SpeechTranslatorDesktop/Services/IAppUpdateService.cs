namespace SpeechTranslatorDesktop.Services;

public interface IAppUpdateService
{
    Task<AppUpdateResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default);

    Task<AppUpdateResult> DownloadUpdateAsync(AppUpdatePackage package, IProgress<int>? progress = null, CancellationToken cancellationToken = default);

    void ApplyUpdateAndRestart(AppUpdatePackage package);

    void OpenExternalInstaller(AppUpdatePackage package);
}
