namespace SpeechTranslatorDesktop.Services;

public enum AppUpdateStatus
{
    UpToDate,
    UpdateAvailable,
    UpdateReadyToInstall,
    ExternalInstallerRequired,
    Failed
}

public sealed record AppUpdatePackage(
    string Version,
    bool RequiresExternalInstaller,
    string? ExternalInstallerUrl,
    object? NativeHandle = null);

public sealed record AppUpdateResult(
    AppUpdateStatus Status,
    string Message,
    AppUpdatePackage? Package = null);
