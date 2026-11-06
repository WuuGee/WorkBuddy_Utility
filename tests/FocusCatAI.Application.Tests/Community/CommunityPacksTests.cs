using FluentAssertions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace FocusCatAI.Application.Tests.Community;

public sealed class CommunityPacksTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wb-community-" + Guid.NewGuid().ToString("N"));

    private IAppPaths Paths()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.RootDirectory.Returns(_root);
        paths.CustomPetsDirectory.Returns(Path.Combine(_root, "Pets"));
        paths.CustomThemesDirectory.Returns(Path.Combine(_root, "Themes"));
        return paths;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private void MakePack(string id, string json, bool png = true)
    {
        var dir = Path.Combine(_root, "Pets", id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "pet.json"), json);
        if (png)
        {
            File.WriteAllBytes(Path.Combine(dir, "idle.png"), [1, 2, 3]);
        }
    }

    private const string Valid = """{"displayName":"Blobby","animations":{"Idle":{"sheet":"idle.png","frames":1}}}""";

    [Fact]
    public void NoCustomPetsKeepsThreeBuiltIns() =>
        new StaticPetCatalog(Paths()).All.Select(p => p.Id).Should().Equal("cat", "tuxedo", "ginger");

    [Fact]
    public void ValidCustomPetIsDiscovered()
    {
        MakePack("blob", Valid);
        var all = new StaticPetCatalog(Paths()).All;
        var pet = all.Single(p => p.Id == "blob");
        pet.DisplayName.Should().Be("Blobby");
        pet.RequiredLevel.Should().Be(1);
        all.Should().HaveCount(4);
    }

    [Fact]
    public void DisplayNameDefaultsToFolderName()
    {
        MakePack("plain", """{"animations":{"Idle":{"sheet":"idle.png"}}}""");
        new StaticPetCatalog(Paths()).All.Single(p => p.Id == "plain").DisplayName.Should().Be("plain");
    }

    [Fact]
    public void BuiltInIdsCannotBeHijacked()
    {
        MakePack("tuxedo", Valid);
        var all = new StaticPetCatalog(Paths()).All;
        all.Should().HaveCount(3);
        all.Single(p => p.Id == "tuxedo").RequiredLevel.Should().Be(2);
    }

    [Fact]
    public void CorruptManifestOrMissingPngIsIgnored()
    {
        MakePack("corrupt", "{not json");
        MakePack("nopng", Valid, png: false);
        MakePack("noidle", """{"animations":{"Walk":{"sheet":"idle.png"}}}""");
        var act = () => new StaticPetCatalog(Paths()).All;
        act.Should().NotThrow();
        act().Should().HaveCount(3);
    }

    [Fact]
    public void ThemeParserAcceptsValidBrushOverrides()
    {
        var result = CustomThemeLoader.Parse("""{"Brush.Accent":"#FF7C4DFF","Brush.Back":"#112233"}""");
        result.Should().HaveCount(2);
        result["Brush.Accent"].Should().Be("#FF7C4DFF");
    }

    [Fact]
    public void ThemeParserIgnoresInvalidEntries()
    {
        var result = CustomThemeLoader.Parse("""{"Accent":"#FFFFFF","Brush.A":"red","Brush.B":"#12345","Brush.C":5,"Brush.D":"#ABCDEF"}""");
        result.Keys.Should().Equal("Brush.D");
    }

    [Theory]
    [InlineData("{broken")]
    [InlineData("[1,2]")]
    [InlineData("")]
    [InlineData(null)]
    public void ThemeParserIgnoresMalformedJson(string? json)
    {
        var act = () => CustomThemeLoader.Parse(json);
        act.Should().NotThrow();
        act().Should().BeEmpty();
    }

    [Fact]
    public void ThemeLoaderReadsFileAndToleratesMissingOrCorrupt()
    {
        var paths = Paths();
        CustomThemeLoader.Load(paths).Should().BeEmpty();
        Directory.CreateDirectory(paths.CustomThemesDirectory);
        File.WriteAllText(Path.Combine(paths.CustomThemesDirectory, CustomThemeLoader.FileName), """{"Brush.X":"#010203"}""");
        CustomThemeLoader.Load(paths).Should().ContainKey("Brush.X");
        File.WriteAllText(Path.Combine(paths.CustomThemesDirectory, CustomThemeLoader.FileName), "garbage");
        CustomThemeLoader.Load(paths).Should().BeEmpty();
    }

    [Fact]
    public void LauncherCustomFoldersDoNotThrowOnBadPaths()
    {
        var paths = Substitute.For<IAppPaths>();
        paths.CustomPetsDirectory.Returns("::invalid\0path");
        paths.CustomThemesDirectory.Returns("::invalid\0path");
        var launcher = new ShellLauncher(paths, NullLogger<ShellLauncher>.Instance);
        var act = () =>
        {
            launcher.OpenCustomPetsFolder();
            launcher.OpenCustomThemesFolder();
        };
        act.Should().NotThrow();
    }
}
