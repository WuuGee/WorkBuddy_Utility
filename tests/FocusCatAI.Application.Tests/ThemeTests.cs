using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using FluentAssertions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Infrastructure.Theme;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace FocusCatAI.Application.Tests;

public class ThemeResolverTests
{
    [Theory]
    [InlineData(ThemePreference.System, true, EffectiveTheme.Light)]
    [InlineData(ThemePreference.System, false, EffectiveTheme.Dark)]
    [InlineData(ThemePreference.Light, false, EffectiveTheme.Light)]
    [InlineData(ThemePreference.Dark, true, EffectiveTheme.Dark)]
    public void ResolvesPreference(ThemePreference preference, bool systemLight, EffectiveTheme expected) =>
        ThemeResolver.Resolve(preference, systemLight).Should().Be(expected);
}

public class ThemeSettingsTests
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    private static ThemePreference Load(string themeJson) =>
        JsonSerializer.Deserialize<AppSettings>($$"""{ "General": { "Theme": {{themeJson}} } }""", Options)!.General.Theme;

    [Fact]
    public void DefaultIsSystem() => new AppSettings().General.Theme.Should().Be(ThemePreference.System);

    [Fact]
    public void MissingThemeLoadsAsSystem() =>
        JsonSerializer.Deserialize<AppSettings>("""{ "General": { "StartMinimized": true } }""", Options)!
            .General.Theme.Should().Be(ThemePreference.System);

    [Theory]
    [InlineData("\"Dark\"", ThemePreference.Dark)]
    [InlineData("\"light\"", ThemePreference.Light)]
    [InlineData("2", ThemePreference.Dark)]
    [InlineData("\"Neon\"", ThemePreference.System)]
    [InlineData("99", ThemePreference.System)]
    [InlineData("-1", ThemePreference.System)]
    [InlineData("true", ThemePreference.System)]
    [InlineData("null", ThemePreference.System)]
    [InlineData("{ \"x\": 1 }", ThemePreference.System)]
    [InlineData("[1, 2]", ThemePreference.System)]
    public void ReadsOrFallsBackToSystem(string json, ThemePreference expected) => Load(json).Should().Be(expected);

    [Theory]
    [InlineData(ThemePreference.System)]
    [InlineData(ThemePreference.Light)]
    [InlineData(ThemePreference.Dark)]
    public void RoundTripsAsString(ThemePreference theme)
    {
        var json = JsonSerializer.Serialize(new AppSettings { General = new GeneralSettings { Theme = theme } });
        json.Should().Contain($"\"Theme\":\"{theme}\"");
        JsonSerializer.Deserialize<AppSettings>(json, Options)!.General.Theme.Should().Be(theme);
    }
}

public class WindowsSystemThemeProviderTests
{
    private static bool Prefers(Func<object?> read) =>
        new WindowsSystemThemeProvider(NullLogger<WindowsSystemThemeProvider>.Instance, read).PrefersLightApps();

    [Fact]
    public void ValueOneIsLight() => Prefers(() => 1).Should().BeTrue();

    [Fact]
    public void ValueZeroIsDark() => Prefers(() => 0).Should().BeFalse();

    [Fact]
    public void MissingKeyFallsBackToLight() => Prefers(() => null).Should().BeTrue();

    [Fact]
    public void UnexpectedTypeFallsBackToLight() => Prefers(() => "dark").Should().BeTrue();

    [Fact]
    public void ReaderFailureFallsBackToLight() =>
        Prefers(() => throw new UnauthorizedAccessException()).Should().BeTrue();
}

public class ThemeXamlTests
{
    private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

    private static string PresentationRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "FocusCatAI.Presentation")))
        {
            dir = dir.Parent;
        }

        return Path.Combine(dir!.FullName, "src", "FocusCatAI.Presentation");
    }

    private static string Read(string relative) => File.ReadAllText(Path.Combine(PresentationRoot(), relative));

    private static string[] Keys(string relative) =>
        XDocument.Parse(Read(relative)).Descendants().Select(e => (string?)e.Attribute(X + "Key")).Where(k => k is not null).Select(k => k!).Order().ToArray();

    [Fact]
    public void LightAndDarkDefineTheSameKeys() =>
        Keys(@"Themes\Theme.Dark.xaml").Should().Equal(Keys(@"Themes\Theme.Light.xaml"));

    [Fact]
    public void ThemesDefineAtLeastTheCoreBrushes() =>
        Keys(@"Themes\Theme.Light.xaml").Should().Contain(["Brush.Window.Background", "Brush.Text.Primary", "Brush.Accent"]);

    [Theory]
    [InlineData(@"Views\SettingsWindow.xaml")]
    [InlineData(@"Views\AboutWindow.xaml")]
    [InlineData(@"Themes\Controls.xaml")]
    public void ThemedXamlHasNoLiteralColors(string file) =>
        Regex.Matches(Read(file), "#[0-9A-Fa-f]{6,8}\\b").Should().BeEmpty();

    [Theory]
    [InlineData(@"Views\SettingsWindow.xaml")]
    [InlineData(@"Views\AboutWindow.xaml")]
    [InlineData(@"Themes\Controls.xaml")]
    public void ThemedXamlUsesNoStaticThemeBrushes(string file) =>
        Regex.Matches(Read(file), @"StaticResource\s+Brush\.").Should().BeEmpty();

    [Fact]
    public void EveryDynamicBrushReferenceExistsInTheTheme()
    {
        var defined = Keys(@"Themes\Theme.Light.xaml").ToHashSet();
        var used = new[] { @"Views\SettingsWindow.xaml", @"Views\AboutWindow.xaml", @"Themes\Controls.xaml" }
            .SelectMany(f => Regex.Matches(Read(f), @"DynamicResource\s+(Brush\.[\w.]+)").Select(m => m.Groups[1].Value))
            .Distinct();
        used.Where(k => !defined.Contains(k)).Should().BeEmpty();
    }

    [Theory]
    [InlineData("PART_Popup")]
    [InlineData("PART_Track")]
    [InlineData("PART_ContentHost")]
    public void ControlTemplatesKeepRequiredParts(string part) =>
        Read(@"Themes\Controls.xaml").Should().Contain($"x:Name=\"{part}\"");

    [Theory]
    [InlineData(@"Views\PetWindow.xaml")]
    [InlineData(@"Views\SpeechBubbleWindow.xaml")]
    public void PetWindowsAreNotThemed(string file) =>
        Read(file).Should().NotContain("DynamicResource");
}
