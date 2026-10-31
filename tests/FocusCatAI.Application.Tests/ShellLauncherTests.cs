using FluentAssertions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Infrastructure;
using FocusCatAI.Shared;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace FocusCatAI.Application.Tests;

public sealed class ShellLauncherTests
{
    [Theory]
    [InlineData("https://github.com/WuuGee/WorkBuddy_Utility/issues")]
    [InlineData("https://github.com/WuuGee/WorkBuddy_Utility/releases/tag/v0.2.0")]
    public void HttpsUrlsAreAllowed(string url) => ShellLauncher.IsAllowedUrl(url).Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("http://github.com")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("C:\\Windows\\System32\\cmd.exe")]
    [InlineData("ms-settings:startupapps")]
    [InlineData("not a url")]
    public void NonHttpsTargetsAreRefused(string? url) => ShellLauncher.IsAllowedUrl(url).Should().BeFalse();

    [Fact]
    public void RefusedUrlDoesNotThrow()
    {
        var launcher = new ShellLauncher(Substitute.For<IAppPaths>(), NullLogger<ShellLauncher>.Instance);

        var act = () => launcher.OpenUrl("file:///C:/Windows/System32/cmd.exe");

        act.Should().NotThrow();
    }

    [Fact]
    public void UnusableLogsPathDoesNotThrow()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.LogsDirectory.Returns("::invalid\0path");
        var launcher = new ShellLauncher(paths, NullLogger<ShellLauncher>.Instance);

        var act = () => launcher.OpenLogsFolder();

        act.Should().NotThrow();
    }

    [Fact]
    public void IssuesUrlIsTheRepositoryIssuesPage() =>
        AppInfo.IssuesUrl.Should().Be("https://github.com/WuuGee/WorkBuddy_Utility/issues");

    [Fact]
    public void IssuesUrlPassesTheLauncherCheck() => ShellLauncher.IsAllowedUrl(AppInfo.IssuesUrl).Should().BeTrue();
}
