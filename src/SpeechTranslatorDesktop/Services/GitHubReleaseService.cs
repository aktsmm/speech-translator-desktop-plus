using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SpeechTranslatorDesktop.Services;

public interface IGitHubReleaseService
{
    Task<GitHubLatestRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default);
}

public sealed record GitHubReleaseAssetInfo(string Name, string BrowserDownloadUrl);

public sealed record GitHubLatestRelease(string TagName, IReadOnlyList<GitHubReleaseAssetInfo> Assets);

public sealed class GitHubReleaseService : IGitHubReleaseService
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repository;

    public GitHubReleaseService(HttpClient httpClient, string owner, string repository)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GitHubLatestRelease> GetLatestReleaseAsync(CancellationToken cancellationToken = default)
    {
        var endpoint = $"https://api.github.com/repos/{_owner}/{_repository}/releases/latest";
        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.UserAgent.ParseAdd("SpeechTranslatorDesktopPlus/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<GitHubLatestReleasePayload>(cancellationToken: cancellationToken);
        if (payload is null || string.IsNullOrWhiteSpace(payload.TagName))
        {
            throw new InvalidOperationException("GitHub release metadata is invalid.");
        }

        var assets = payload.Assets?
            .Where(asset => !string.IsNullOrWhiteSpace(asset.Name) && !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
            .Select(asset => new GitHubReleaseAssetInfo(asset.Name!, asset.BrowserDownloadUrl!))
            .ToArray() ?? [];

        return new GitHubLatestRelease(payload.TagName, assets);
    }

    private sealed class GitHubLatestReleasePayload
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("assets")]
        public GitHubReleaseAssetPayload[]? Assets { get; set; }
    }

    private sealed class GitHubReleaseAssetPayload
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
