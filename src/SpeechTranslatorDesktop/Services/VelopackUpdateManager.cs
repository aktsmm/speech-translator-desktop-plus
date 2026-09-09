using Velopack;
using Velopack.Sources;
using System.Reflection;

namespace SpeechTranslatorDesktop.Services;

public sealed record VelopackUpdateReference(string Version, object NativeHandle);

public interface IVelopackUpdateManager
{
    bool IsInstalled { get; }
    string CurrentVersion { get; }
    VelopackUpdateReference? PendingRestartUpdate { get; }
    Task<VelopackUpdateReference?> CheckForUpdatesAsync(CancellationToken cancellationToken = default);
    Task DownloadUpdatesAsync(VelopackUpdateReference update, Action<int>? progress = null, CancellationToken cancellationToken = default);
    void ApplyUpdatesAndRestart(VelopackUpdateReference update);
}

public sealed class VelopackUpdateManager : IVelopackUpdateManager
{
    private readonly IVelopackNativeManager _nativeManager;
    private readonly string _fallbackVersion;

    public VelopackUpdateManager(string repositoryUrl, bool includePrerelease = false)
        : this(CreateNativeManager(repositoryUrl, includePrerelease), GetAssemblyInformationalVersion())
    { }

    internal VelopackUpdateManager(IVelopackNativeManager nativeManager, string fallbackVersion)
    {
        _nativeManager = nativeManager ?? throw new ArgumentNullException(nameof(nativeManager));
        _fallbackVersion = string.IsNullOrWhiteSpace(fallbackVersion) ? "0.0.0" : fallbackVersion;
    }

    public bool IsInstalled => _nativeManager.IsInstalled;

    public string CurrentVersion => _nativeManager.CurrentVersion?.ToString() ?? _fallbackVersion;

    public VelopackUpdateReference? PendingRestartUpdate
    {
        get
        {
            var pending = _nativeManager.UpdatePendingRestart;
            return pending is null
                ? null
                : new VelopackUpdateReference(pending.Version.ToString(), pending);
        }
    }

    public async Task<VelopackUpdateReference?> CheckForUpdatesAsync(CancellationToken cancellationToken = default)
    {
        var updateInfo = await _nativeManager.CheckForUpdatesAsync(cancellationToken);
        return updateInfo is null
            ? null
            : new VelopackUpdateReference(updateInfo.TargetFullRelease.Version.ToString(), updateInfo);
    }

    public Task DownloadUpdatesAsync(VelopackUpdateReference update, Action<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (update.NativeHandle is not UpdateInfo native)
        {
            throw new ArgumentException("Native update handle is invalid.", nameof(update));
        }

        return _nativeManager.DownloadUpdatesAsync(native, progress, cancellationToken);
    }

    public void ApplyUpdatesAndRestart(VelopackUpdateReference update)
    {
        var toApply = update.NativeHandle switch
        {
            VelopackAsset asset => asset,
            UpdateInfo info => info.TargetFullRelease,
            _ => throw new ArgumentException("Native update handle is invalid.", nameof(update))
        };

        _nativeManager.ApplyUpdatesAndRestart(toApply);
    }

    private static string GetAssemblyInformationalVersion()
    {
        var assembly = Assembly.GetEntryAssembly() ?? typeof(VelopackUpdateManager).Assembly;
        var informationalVersion = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return string.IsNullOrWhiteSpace(informationalVersion) ? "0.0.0" : informationalVersion;
    }

    private static IVelopackNativeManager CreateNativeManager(string repositoryUrl, bool includePrerelease)
    {
        if (string.IsNullOrWhiteSpace(repositoryUrl))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(repositoryUrl));
        }

        return new NativeVelopackManager(new UpdateManager(new GithubSource(repositoryUrl, accessToken: null, prerelease: includePrerelease)));
    }

    internal interface IVelopackNativeManager
    {
        bool IsInstalled { get; }
        SemanticVersion? CurrentVersion { get; }
        VelopackAsset? UpdatePendingRestart { get; }
        Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken);
        Task DownloadUpdatesAsync(UpdateInfo update, Action<int>? progress, CancellationToken cancellationToken);
        void ApplyUpdatesAndRestart(VelopackAsset update);
    }

    private sealed class NativeVelopackManager : IVelopackNativeManager
    {
        private readonly UpdateManager _updateManager;

        public NativeVelopackManager(UpdateManager updateManager)
        {
            _updateManager = updateManager;
        }

        public bool IsInstalled => _updateManager.IsInstalled;
        public SemanticVersion? CurrentVersion => _updateManager.CurrentVersion;
        public VelopackAsset? UpdatePendingRestart => _updateManager.UpdatePendingRestart;

        public Task<UpdateInfo?> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            return _updateManager.CheckForUpdatesAsync();
        }

        public Task DownloadUpdatesAsync(UpdateInfo update, Action<int>? progress, CancellationToken cancellationToken)
        {
            return _updateManager.DownloadUpdatesAsync(update, progress, cancellationToken);
        }

        public void ApplyUpdatesAndRestart(VelopackAsset update)
        {
            _updateManager.ApplyUpdatesAndRestart(update);
        }
    }
}
