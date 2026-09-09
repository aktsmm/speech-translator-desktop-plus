using System.Net.Http;
using SpeechTranslatorDesktop.Services;

namespace SpeechTranslator.Desktop.Tests;

public class AppUpdateServiceTests
{
    [Fact]
    public async Task CheckForUpdates_WhenManagedInstallHasUpdate_ReturnsUpdateAvailable()
    {
        var service = new AppUpdateService(
            new FakeVelopackUpdateManager
            {
                IsInstalled = true,
                CurrentVersion = "1.8.9",
                AvailableUpdate = new VelopackUpdateReference("1.9.0", new object())
            },
            new FakeGitHubReleaseService());

        var result = await service.CheckForUpdatesAsync();

        result.Status.Should().Be(AppUpdateStatus.UpdateAvailable);
        result.Package.Should().NotBeNull();
        result.Package!.Version.Should().Be("1.9.0");
        result.Package.RequiresExternalInstaller.Should().BeFalse();
    }

    [Fact]
    public async Task CheckForUpdates_WhenPortableAndNewerVersionWithoutInstaller_ReturnsFailed()
    {
        var service = new AppUpdateService(
            new FakeVelopackUpdateManager
            {
                IsInstalled = false,
                CurrentVersion = "1.8.9"
            },
            new FakeGitHubReleaseService
            {
                LatestRelease = new GitHubLatestRelease(
                    "v1.9.0",
                    [
                        new GitHubReleaseAssetInfo("SpeechTranslatorDesktopPlus-win-x64.zip", "https://example.invalid/zip")
                    ])
            });

        var result = await service.CheckForUpdatesAsync();

        result.Status.Should().Be(AppUpdateStatus.Failed);
        result.Message.ToLowerInvariant().Should().Contain("installer");
    }

    [Fact]
    public async Task CheckForUpdates_WhenPortableAndLatestReleaseIsOlder_ReturnsUpToDate()
    {
        var service = new AppUpdateService(
            new FakeVelopackUpdateManager
            {
                IsInstalled = false,
                CurrentVersion = "1.9.0"
            },
            new FakeGitHubReleaseService
            {
                LatestRelease = new GitHubLatestRelease(
                    "v1.8.9",
                    [
                        new GitHubReleaseAssetInfo("SpeechTranslatorDesktopPlus-win-Setup.exe", "https://github.com/aktsmm/speech-translator-desktop-plus/releases/download/v1.8.9/SpeechTranslatorDesktopPlus-win-Setup.exe")
                    ])
            });

        var result = await service.CheckForUpdatesAsync();

        result.Status.Should().Be(AppUpdateStatus.UpToDate);
    }

    [Fact]
    public async Task CheckForUpdates_WhenPortableAndSameVersionWithInstaller_ReturnsInstallerMigration()
    {
        var service = new AppUpdateService(
            new FakeVelopackUpdateManager
            {
                IsInstalled = false,
                CurrentVersion = "1.9.0"
            },
            new FakeGitHubReleaseService
            {
                LatestRelease = new GitHubLatestRelease(
                    "v1.9.0",
                    [
                        new GitHubReleaseAssetInfo("SpeechTranslatorDesktopPlus-win-Setup.exe", "https://github.com/aktsmm/speech-translator-desktop-plus/releases/download/v1.9.0/SpeechTranslatorDesktopPlus-win-Setup.exe")
                    ])
            });

        var result = await service.CheckForUpdatesAsync();

        result.Status.Should().Be(AppUpdateStatus.ExternalInstallerRequired);
        result.Package.Should().NotBeNull();
        result.Package!.RequiresExternalInstaller.Should().BeTrue();
    }

    [Fact]
    public async Task CheckForUpdates_WhenPortableTagIsInvalid_ReturnsFailed()
    {
        var service = new AppUpdateService(
            new FakeVelopackUpdateManager
            {
                IsInstalled = false,
                CurrentVersion = "1.9.0"
            },
            new FakeGitHubReleaseService
            {
                LatestRelease = new GitHubLatestRelease(
                    "this-is-not-semver",
                    [
                        new GitHubReleaseAssetInfo("SpeechTranslatorDesktopPlus-win-Setup.exe", "https://github.com/aktsmm/speech-translator-desktop-plus/releases/download/v1.9.0/SpeechTranslatorDesktopPlus-win-Setup.exe")
                    ])
            });

        var result = await service.CheckForUpdatesAsync();

        result.Status.Should().Be(AppUpdateStatus.Failed);
        result.Message.Should().Contain("metadata");
    }

    [Fact]
    public async Task CheckForUpdates_WhenPortableReleaseIsPrereleaseAndCurrentIsStable_ReturnsUpToDate()
    {
        var service = new AppUpdateService(
            new FakeVelopackUpdateManager
            {
                IsInstalled = false,
                CurrentVersion = "1.9.0"
            },
            new FakeGitHubReleaseService
            {
                LatestRelease = new GitHubLatestRelease(
                    "v1.9.0-beta.1",
                    [
                        new GitHubReleaseAssetInfo("SpeechTranslatorDesktopPlus-win-Setup.exe", "https://github.com/aktsmm/speech-translator-desktop-plus/releases/download/v1.9.0-beta.1/SpeechTranslatorDesktopPlus-win-Setup.exe")
                    ])
            });

        var result = await service.CheckForUpdatesAsync();

        result.Status.Should().Be(AppUpdateStatus.UpToDate);
    }

    [Fact]
    public async Task CheckForUpdates_WhenInstallerUrlIsOutsideOfficialDownloadPath_ReturnsFailed()
    {
        var service = new AppUpdateService(
            new FakeVelopackUpdateManager
            {
                IsInstalled = false,
                CurrentVersion = "1.8.9"
            },
            new FakeGitHubReleaseService
            {
                LatestRelease = new GitHubLatestRelease(
                    "v1.9.0",
                    [
                        new GitHubReleaseAssetInfo("SpeechTranslatorDesktopPlus-win-Setup.exe", "https://example.invalid/setup.exe")
                    ])
            });

        var result = await service.CheckForUpdatesAsync();

        result.Status.Should().Be(AppUpdateStatus.Failed);
        result.Message.Should().Contain("installer");
    }

    [Fact]
    public async Task CheckForUpdates_WhenReleaseFetchFails_ReturnsFailed()
    {
        var service = new AppUpdateService(
            new FakeVelopackUpdateManager
            {
                IsInstalled = false,
                CurrentVersion = "1.8.9"
            },
            new FakeGitHubReleaseService
            {
                LoadException = new HttpRequestException("network down")
            });

        var result = await service.CheckForUpdatesAsync();

        result.Status.Should().Be(AppUpdateStatus.Failed);
        result.Message.Should().Contain("network down");
    }

    [Fact]
    public async Task CheckForUpdates_WhenCancelled_PropagatesCancellation()
    {
        var service = new AppUpdateService(
            new FakeVelopackUpdateManager
            {
                IsInstalled = false,
                CurrentVersion = "1.8.9"
            },
            new FakeGitHubReleaseService
            {
                LoadException = new OperationCanceledException()
            });

        var act = () => service.CheckForUpdatesAsync(new CancellationToken(canceled: true));

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task DownloadUpdate_WhenManagedUpdateSelected_StartsDownload()
    {
        var manager = new FakeVelopackUpdateManager
        {
            IsInstalled = true,
            CurrentVersion = "1.8.9",
            AvailableUpdate = new VelopackUpdateReference("1.9.0", new object())
        };
        var service = new AppUpdateService(manager, new FakeGitHubReleaseService());
        var checkResult = await service.CheckForUpdatesAsync();

        var result = await service.DownloadUpdateAsync(checkResult.Package!, new Progress<int>());

        manager.DownloadCallCount.Should().Be(1);
        result.Status.Should().Be(AppUpdateStatus.UpdateReadyToInstall);
    }

    private sealed class FakeVelopackUpdateManager : IVelopackUpdateManager
    {
        public bool IsInstalled { get; init; }
        public string CurrentVersion { get; init; } = "1.8.9";
        public VelopackUpdateReference? AvailableUpdate { get; init; }
        public VelopackUpdateReference? PendingRestartUpdate { get; init; }
        public int DownloadCallCount { get; private set; }

        public Task<VelopackUpdateReference?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(AvailableUpdate);
        }

        public Task DownloadUpdatesAsync(VelopackUpdateReference update, Action<int>? progress = null, CancellationToken cancellationToken = default)
        {
            DownloadCallCount++;
            progress?.Invoke(100);
            return Task.CompletedTask;
        }

        public void ApplyUpdatesAndRestart(VelopackUpdateReference update)
        {
        }
    }

    private sealed class FakeGitHubReleaseService : IGitHubReleaseService
    {
        public Exception? LoadException { get; init; }
        public GitHubLatestRelease LatestRelease { get; init; } = new(
            "v1.8.9",
            [new GitHubReleaseAssetInfo("SpeechTranslatorDesktopPlus-win-Setup.exe", "https://github.com/aktsmm/speech-translator-desktop-plus/releases/download/v1.8.9/SpeechTranslatorDesktopPlus-win-Setup.exe")]);

        public Task<GitHubLatestRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
        {
            if (LoadException is not null)
            {
                throw LoadException;
            }

            return Task.FromResult(LatestRelease);
        }
    }
}
