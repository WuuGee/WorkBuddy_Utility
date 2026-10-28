using System.Net;
using FluentAssertions;
using FocusCatAI.Application.Updates;
using FocusCatAI.Infrastructure.Updates;
using FocusCatAI.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusCatAI.Application.Tests;

public sealed class TrayAndUpdateTests
{
    [Theory]
    [InlineData("v0.2.0", "0.2.0")]
    [InlineData("0.2.0", "0.2.0")]
    [InlineData("V1.0.0", "1.0.0")]
    [InlineData("v1.2.3-beta.1", "1.2.3")]
    [InlineData("v1.2.3+build5", "1.2.3")]
    [InlineData("1.2", "1.2.0")]
    public void ReleaseTagsAreParsed(string tag, string expected)
    {
        ReleaseVersion.Parse(tag).Should().Be(Version.Parse(expected));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("release-candidate")]
    public void UnreadableTagsGiveNull(string? tag)
    {
        ReleaseVersion.Parse(tag).Should().BeNull();
    }

    [Fact]
    public void FourPartVersionsCompareEqualToThreePart()
    {
        ReleaseVersion.Normalize(new Version(0, 1, 0, 0)).Should().Be(new Version(0, 1, 0));
    }

    [Fact]
    public async Task NewerReleaseIsReported()
    {
        var result = await Check(HttpStatusCode.OK, "{\"tag_name\":\"v0.2.0\",\"html_url\":\"https://github.com/WuuGee/WorkBuddy_Utility/releases/tag/v0.2.0\"}");
        result.Outcome.Should().Be(UpdateOutcome.UpdateAvailable);
        result.Latest.Should().Be(new Version(0, 2, 0));
        result.ReleaseUrl.Should().Contain("v0.2.0");
    }

    [Fact]
    public async Task SameReleaseIsUpToDate()
    {
        (await Check(HttpStatusCode.OK, "{\"tag_name\":\"v0.1.0\"}")).Outcome.Should().Be(UpdateOutcome.UpToDate);
    }

    [Fact]
    public async Task OlderReleaseIsUpToDate()
    {
        (await Check(HttpStatusCode.OK, "{\"tag_name\":\"v0.0.9\"}")).Outcome.Should().Be(UpdateOutcome.UpToDate);
    }

    [Fact]
    public async Task NoPublishedReleaseIsNotAnError()
    {
        (await Check(HttpStatusCode.NotFound, "{}")).Outcome.Should().Be(UpdateOutcome.NoReleases);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task ServerErrorsFailGracefully(HttpStatusCode status)
    {
        var result = await Check(status, "{}");
        result.Outcome.Should().Be(UpdateOutcome.Failed);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GarbageBodyFailsGracefully()
    {
        (await Check(HttpStatusCode.OK, "not json")).Outcome.Should().Be(UpdateOutcome.Failed);
    }

    [Fact]
    public async Task UnreadableTagFailsGracefully()
    {
        (await Check(HttpStatusCode.OK, "{\"tag_name\":\"nightly\"}")).Outcome.Should().Be(UpdateOutcome.Failed);
    }

    [Fact]
    public async Task OfflineFailsGracefully()
    {
        var checker = new GitHubUpdateChecker(new HttpClient(new StubHandler(throws: true)), NullLogger<GitHubUpdateChecker>.Instance, "https://example.test/x");
        var result = await checker.CheckAsync(new Version(0, 1, 0));
        result.Outcome.Should().Be(UpdateOutcome.Failed);
    }

    [Fact]
    public async Task ReleaseUrlOutsideGitHubIsNeverOpened()
    {
        var result = await Check(HttpStatusCode.OK, "{\"tag_name\":\"v0.2.0\",\"html_url\":\"https://evil.example/download\"}");
        result.ReleaseUrl.Should().Be(AppInfo.ReleasesPageUrl);
    }

    [Fact]
    public async Task NonHttpsReleaseUrlIsNeverOpened()
    {
        var result = await Check(HttpStatusCode.OK, "{\"tag_name\":\"v0.2.0\",\"html_url\":\"file:///c:/windows/system32/cmd.exe\"}");
        result.ReleaseUrl.Should().Be(AppInfo.ReleasesPageUrl);
    }

    [Fact]
    public void RestartArgumentCarriesTheOldProcessId()
    {
        StartupOptions.Parse(["--restart-after", "4242"], null).RestartAfterProcessId.Should().Be(4242);
    }

    [Theory]
    [InlineData("--restart-after")]
    public void RestartArgumentWithoutIdIsIgnored(string arg)
    {
        StartupOptions.Parse([arg], null).RestartAfterProcessId.Should().BeNull();
    }

    [Fact]
    public void RestartArgumentWithBadIdIsIgnored()
    {
        StartupOptions.Parse(["--restart-after", "abc"], null).RestartAfterProcessId.Should().BeNull();
    }

    [Fact]
    public void VerifyTrayStartsPhaseOne()
    {
        StartupOptions.Parse(["--verify-tray"], null).TrayPhase.Should().Be(1);
    }

    [Theory]
    [InlineData("2", 2)]
    [InlineData("3", 3)]
    [InlineData("1", 0)]
    [InlineData("9", 0)]
    [InlineData("x", 0)]
    [InlineData(null, 0)]
    public void TrayPhasesTwoAndThreeComeFromTheEnvironment(string? env, int expected)
    {
        StartupOptions.Parse([], env).TrayPhase.Should().Be(expected);
    }

    [Fact]
    public void AutostartNeverRunsTheTrayVerifier()
    {
        StartupOptions.Parse(["--autostart", "--verify-tray"], "2").TrayPhase.Should().Be(0);
    }

    private static async Task<UpdateCheckResult> Check(HttpStatusCode status, string body)
    {
        var checker = new GitHubUpdateChecker(new HttpClient(new StubHandler(status, body)), NullLogger<GitHubUpdateChecker>.Instance, "https://example.test/x");
        return await checker.CheckAsync(new Version(0, 1, 0));
    }

    private sealed class StubHandler(HttpStatusCode status = HttpStatusCode.OK, string body = "{}", bool throws = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (throws)
            {
                throw new HttpRequestException("offline");
            }

            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
        }
    }
}
