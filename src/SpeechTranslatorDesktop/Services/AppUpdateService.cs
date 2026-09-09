using System.Diagnostics;
using System.Net.Http;
using Velopack;

namespace SpeechTranslatorDesktop.Services;

public sealed class AppUpdateService : IAppUpdateService
{
    private const string DefaultRepositoryOwner = "aktsmm";
    private const string DefaultRepositoryName = "speech-translator-desktop-plus";
    private const string SetupAssetName = "SpeechTranslatorDesktopPlus-win-Setup.exe";
    private readonly IVelopackUpdateManager _updateManager;
    private readonly IGitHubReleaseService _gitHubReleaseService;
    private readonly string _repositoryOwner;
    private readonly string _repositoryName;

    public AppUpdateService(
        IVelopackUpdateManager updateManager,
        IGitHubReleaseService gitHubReleaseService,
        string repositoryOwner = DefaultRepositoryOwner,
        string repositoryName = DefaultRepositoryName)
    {
        _updateManager = updateManager ?? throw new ArgumentNullException(nameof(updateManager));
        _gitHubReleaseService = gitHubReleaseService ?? throw new ArgumentNullException(nameof(gitHubReleaseService));
        _repositoryOwner = repositoryOwner ?? throw new ArgumentNullException(nameof(repositoryOwner));
        _repositoryName = repositoryName ?? throw new ArgumentNullException(nameof(repositoryName));
    }

    public async Task<AppUpdateResult> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_updateManager.IsInstalled)
            {
                var pending = _updateManager.PendingRestartUpdate;
                if (pending is not null)
                {
                    return new AppUpdateResult(
                        AppUpdateStatus.UpdateReadyToInstall,
                        $"Update {pending.Version} is ready. Restart to install.",
                        new AppUpdatePackage(pending.Version, false, null, pending.NativeHandle));
                }

                var update = await _updateManager.CheckForUpdatesAsync(cancellationToken);
                if (update is null)
                {
                    return new AppUpdateResult(AppUpdateStatus.UpToDate, "You're on the latest version.");
                }

                return new AppUpdateResult(
                    AppUpdateStatus.UpdateAvailable,
                    $"Update {update.Version} is available.",
                    new AppUpdatePackage(update.Version, false, null, update.NativeHandle));
            }

            var release = await _gitHubReleaseService.GetLatestReleaseAsync(cancellationToken);
            if (!TryParseSemanticVersion(release.TagName, out var releaseVersion))
            {
                return new AppUpdateResult(AppUpdateStatus.Failed, $"Release metadata is invalid: tag '{release.TagName}' is not a semantic version.");
            }

            if (!TryParseSemanticVersion(_updateManager.CurrentVersion, out var currentVersion))
            {
                return new AppUpdateResult(AppUpdateStatus.Failed, $"Current app version metadata is invalid: '{_updateManager.CurrentVersion}'.");
            }

            var versionComparison = releaseVersion.CompareTo(currentVersion);
            if (versionComparison < 0)
            {
                return new AppUpdateResult(AppUpdateStatus.UpToDate, "You're on the latest version.");
            }

            var installer = release.Assets.FirstOrDefault(asset =>
                string.Equals(asset.Name, SetupAssetName, StringComparison.OrdinalIgnoreCase));
            if (installer is null || !IsTrustedGitHubReleaseDownloadUrl(installer.BrowserDownloadUrl))
            {
                return new AppUpdateResult(AppUpdateStatus.Failed, "A release exists, but no trusted installer asset was found.");
            }

            return new AppUpdateResult(
                AppUpdateStatus.ExternalInstallerRequired,
                versionComparison == 0
                    ? $"Installer migration is available for version {releaseVersion.ToFullString()}. Open installer download page."
                    : $"Update {releaseVersion.ToFullString()} is available. Open installer download page.",
                new AppUpdatePackage(releaseVersion.ToFullString(), true, installer.BrowserDownloadUrl));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            return new AppUpdateResult(AppUpdateStatus.Failed, $"Update check failed: {ex.Message}");
        }
        catch (InvalidOperationException ex)
        {
            return new AppUpdateResult(AppUpdateStatus.Failed, $"Update check failed: {ex.Message}");
        }
        catch (System.Text.Json.JsonException ex)
        {
            return new AppUpdateResult(AppUpdateStatus.Failed, $"Update check failed: {ex.Message}");
        }
        catch (FormatException ex)
        {
            return new AppUpdateResult(AppUpdateStatus.Failed, $"Update check failed: {ex.Message}");
        }
    }

    public async Task<AppUpdateResult> DownloadUpdateAsync(AppUpdatePackage package, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.RequiresExternalInstaller)
        {
            return new AppUpdateResult(AppUpdateStatus.ExternalInstallerRequired, "This install mode requires downloading the installer from GitHub Releases.", package);
        }

        var update = new VelopackUpdateReference(package.Version, package.NativeHandle ?? throw new ArgumentException("Native handle is required.", nameof(package)));
        await _updateManager.DownloadUpdatesAsync(update, value => progress?.Report(value), cancellationToken);
        return new AppUpdateResult(AppUpdateStatus.UpdateReadyToInstall, $"Update {package.Version} is ready to install.", package);
    }

    public void ApplyUpdateAndRestart(AppUpdatePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (package.RequiresExternalInstaller)
        {
            throw new InvalidOperationException("External-installer mode cannot apply in place.");
        }

        var update = new VelopackUpdateReference(package.Version, package.NativeHandle ?? throw new ArgumentException("Native handle is required.", nameof(package)));
        _updateManager.ApplyUpdatesAndRestart(update);
    }

    public void OpenExternalInstaller(AppUpdatePackage package)
    {
        ArgumentNullException.ThrowIfNull(package);
        if (string.IsNullOrWhiteSpace(package.ExternalInstallerUrl) || !IsTrustedGitHubReleaseDownloadUrl(package.ExternalInstallerUrl))
        {
            throw new InvalidOperationException("Installer URL is missing or untrusted.");
        }

        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = package.ExternalInstallerUrl,
            UseShellExecute = true
        });
    }

    internal static string NormalizeVersionString(string value)
    {
        var withoutPrefix = value.Trim();
        if (withoutPrefix.StartsWith('v') || withoutPrefix.StartsWith('V'))
        {
            withoutPrefix = withoutPrefix[1..];
        }

        return withoutPrefix;
    }

    internal static bool IsNewerVersion(string currentVersion, string candidateVersion)
    {
        if (!TryParseSemanticVersion(currentVersion, out var current))
        {
            return false;
        }

        if (!TryParseSemanticVersion(candidateVersion, out var candidate))
        {
            return false;
        }

        return candidate > current;
    }

    internal static int CompareVersions(string leftVersion, string rightVersion)
    {
        if (!TryParseSemanticVersion(leftVersion, out var left))
        {
            throw new FormatException($"Invalid semantic version: {leftVersion}");
        }

        if (!TryParseSemanticVersion(rightVersion, out var right))
        {
            throw new FormatException($"Invalid semantic version: {rightVersion}");
        }

        return left.CompareTo(right);
    }

    private static bool TryParseSemanticVersion(string value, out SemanticVersion version)
    {
        var normalized = NormalizeVersionString(value);
        if (!SemanticVersion.TryParse(normalized, out var parsed) || parsed is null)
        {
            version = null!;
            return false;
        }

        version = parsed;
        return true;
    }

    private bool IsTrustedGitHubReleaseDownloadUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var expectedPrefix = $"/{_repositoryOwner}/{_repositoryName}/releases/download/";
        if (!uri.AbsolutePath.StartsWith(expectedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.Equals(Path.GetFileName(uri.AbsolutePath), SetupAssetName, StringComparison.OrdinalIgnoreCase);
    }
}
