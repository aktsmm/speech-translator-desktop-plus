using SpeechTranslatorDesktop.Services;
using Velopack;

namespace SpeechTranslator.Desktop.Tests;

public class VelopackUpdateManagerTests
{
    [Fact]
    public void CurrentVersion_WhenRunningAsNonManagedApp_UsesAssemblyVersionFallback()
    {
        VelopackApp.Build()
            .SetAutoApplyOnStartup(false)
            .Run();

        var manager = new VelopackUpdateManager("https://github.com/aktsmm/speech-translator-desktop-plus");

        manager.IsInstalled.Should().BeFalse();
        manager.CurrentVersion.Should().NotBe("0.0.0");
    }

    [Fact]
    public void CurrentVersion_WhenNativeCurrentVersionIsNull_UsesFallbackVersion()
    {
        var manager = new VelopackUpdateManager(new FakeNativeManager(), "1.9.0+test");

        manager.CurrentVersion.Should().Be("1.9.0+test");
    }

    private sealed class FakeNativeManager : VelopackUpdateManager.IVelopackNativeManager
    {
        public bool IsInstalled => false;
        public SemanticVersion? CurrentVersion => null;
        public VelopackAsset? UpdatePendingRestart => null;
        public Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken) => Task.FromResult<UpdateInfo?>(null);
        public Task DownloadUpdatesAsync(UpdateInfo update, Action<int>? progress, CancellationToken cancellationToken) => Task.CompletedTask;
        public void ApplyUpdatesAndRestart(VelopackAsset update) { }
    }
}
