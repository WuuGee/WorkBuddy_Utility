using System.Net;
using System.Text.Json;
using FocusCatAI.Application.Updates;
using FocusCatAI.Shared;
using Microsoft.Extensions.Logging;

namespace FocusCatAI.Infrastructure.Updates;

/// <summary>Asks GitHub for the latest published (non-draft, non-prerelease) release. Read-only; sends no personal data.</summary>
public sealed class GitHubUpdateChecker(HttpClient http, ILogger<GitHubUpdateChecker> logger, string apiUrl = AppInfo.LatestReleaseApiUrl) : IUpdateChecker
{
    public async Task<UpdateCheckResult> CheckAsync(Version current, CancellationToken cancellationToken = default)
    {
        var installed = ReleaseVersion.Normalize(current);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.UserAgent.ParseAdd($"WorkBuddy/{installed}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(UpdateOutcome.NoReleases, installed, null, AppInfo.ReleasesPageUrl, null);
            }

            if (!response.IsSuccessStatusCode)
            {
                return Failed(installed, $"GitHub answered {(int)response.StatusCode}.");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var tag = doc.RootElement.TryGetProperty("tag_name", out var t) ? t.GetString() : null;
            var latest = ReleaseVersion.Parse(tag);
            if (latest is null)
            {
                return Failed(installed, "The latest release has a version tag WorkBuddy cannot read.");
            }

            var url = doc.RootElement.TryGetProperty("html_url", out var u) ? u.GetString() : null;
            url = IsTrustedReleaseUrl(url) ? url : AppInfo.ReleasesPageUrl;

            return latest > installed
                ? new UpdateCheckResult(UpdateOutcome.UpdateAvailable, installed, latest, url, null)
                : new UpdateCheckResult(UpdateOutcome.UpToDate, installed, latest, url, null);
        }
        catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException or JsonException or InvalidOperationException
                                       && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Update check failed");
            return Failed(installed, "Could not reach GitHub. Check your internet connection and try again.");
        }
    }

    // Only ever open a github.com page, whatever the response says.
    private static bool IsTrustedReleaseUrl(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps &&
        uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase);

    private static UpdateCheckResult Failed(Version current, string error) =>
        new(UpdateOutcome.Failed, current, null, null, error);
}
