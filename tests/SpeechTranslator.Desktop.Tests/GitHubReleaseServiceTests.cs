using System.Net;
using System.Net.Http;
using System.Text;
using SpeechTranslatorDesktop.Services;

namespace SpeechTranslator.Desktop.Tests;

public class GitHubReleaseServiceTests
{
    [Fact]
    public async Task GetLatestReleaseAsync_ParsesTagAndAssets()
    {
        var json = """
            {
              "tag_name": "v1.9.0",
              "assets": [
                { "name": "SpeechTranslatorDesktopPlus-win-Setup.exe", "browser_download_url": "https://example.invalid/setup" },
                { "name": "RELEASES", "browser_download_url": "https://example.invalid/releases" }
              ]
            }
            """;
        var client = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.OK, json));
        var service = new GitHubReleaseService(client, "aktsmm", "speech-translator-desktop-plus");

        var result = await service.GetLatestReleaseAsync();

        result.TagName.Should().Be("v1.9.0");
        result.Assets.Should().ContainSingle(asset => asset.Name == "SpeechTranslatorDesktopPlus-win-Setup.exe");
    }

    [Fact]
    public async Task GetLatestReleaseAsync_WhenResponseIsFailure_ThrowsHttpRequestException()
    {
        var client = new HttpClient(new FakeHttpMessageHandler(HttpStatusCode.BadGateway, "{}"));
        var service = new GitHubReleaseService(client, "aktsmm", "speech-translator-desktop-plus");

        var act = () => service.GetLatestReleaseAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    private sealed class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _content;

        public FakeHttpMessageHandler(HttpStatusCode statusCode, string content)
        {
            _statusCode = statusCode;
            _content = content;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_content, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        }
    }
}
