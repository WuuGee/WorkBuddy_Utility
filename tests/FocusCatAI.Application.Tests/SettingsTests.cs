using FluentAssertions;
using System.Text.Json.Nodes;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Application.Settings;
using FocusCatAI.Domain.Pets;
using FocusCatAI.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusCatAI.Application.Tests;

public sealed class SettingsTests : IDisposable
{
    private sealed class TestPaths(string root) : IAppPaths
    {
        public string RootDirectory => root;
        public string LogsDirectory => Path.Combine(root, "logs");
        public string ModelsDirectory => Path.Combine(root, "models");
    }

    private readonly string _root = Path.Combine(Path.GetTempPath(), "wb-settings-" + Guid.NewGuid().ToString("N"));
    private string SettingsFile => Path.Combine(_root, "settings.json");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private JsonSettingsService Create() => new(new TestPaths(_root), NullLogger<JsonSettingsService>.Instance);

    // ---- defaults and store ----

    [Fact]
    public void FirstRun_WritesDefaultSettingsFile()
    {
        Create();

        File.Exists(SettingsFile).Should().BeTrue();
    }

    [Fact]
    public void Defaults_MatchTheAgreedValues()
    {
        var s = Create().Current;

        s.General.StartWithWindows.Should().BeTrue();
        s.Pet.ShowPet.Should().BeTrue();
        s.Pet.Size.Should().Be(PetSize.Normal);
        s.Pet.Placement.Should().Be(PlacementPreference.Auto);
        s.Behavior.SleepDelaySeconds.Should().Be(300);
        s.Behavior.MessageFrequency.Should().Be(MessageFrequency.Normal);
        s.QuietHours.Enabled.Should().BeFalse();
    }

    [Fact]
    public void Update_PersistsAndSurvivesRestart()
    {
        var first = Create();
        first.Update(s => s with { Pet = s.Pet with { Size = PetSize.Large }, Behavior = s.Behavior with { SleepDelaySeconds = 60 } });

        var second = Create();

        second.Current.Pet.Size.Should().Be(PetSize.Large);
        second.Current.Behavior.SleepDelaySeconds.Should().Be(60);
    }

    [Fact]
    public void Update_RaisesChangedOnce_AndNotWhenNothingChanged()
    {
        var sut = Create();
        var raised = 0;
        sut.Changed += (_, _) => raised++;

        sut.Update(s => s with { Pet = s.Pet with { Size = PetSize.Huge } });
        sut.Update(s => s with { Pet = s.Pet with { Size = PetSize.Huge } });

        raised.Should().Be(1);
    }

    [Fact]
    public void SavedFile_IsHumanReadable_WithNamedEnumsAndVersion()
    {
        var sut = Create();
        sut.Update(s => s with { Pet = s.Pet with { Size = PetSize.Large, Placement = PlacementPreference.InTaskbar } });

        var json = File.ReadAllText(SettingsFile);

        json.Should().Contain("\"SchemaVersion\": 1").And.Contain("\"Size\": \"Large\"").And.Contain("\"Placement\": \"InTaskbar\"");
    }

    [Fact]
    public void Save_LeavesNoTempFile_AndKeepsBackupOfPreviousVersion()
    {
        var sut = Create();
        sut.Update(s => s with { Pet = s.Pet with { Size = PetSize.Large } });
        sut.Update(s => s with { Pet = s.Pet with { Size = PetSize.Huge } });

        File.Exists(SettingsFile + ".tmp").Should().BeFalse();
        File.ReadAllText(SettingsFile + ".bak").Should().Contain("\"Large\"");
    }

    [Fact]
    public void ResetToDefaults_RestoresEverything()
    {
        var sut = Create();
        sut.Update(s => s with { Pet = s.Pet with { Size = PetSize.Huge }, QuietHours = s.QuietHours with { Enabled = true } });

        sut.ResetToDefaults();

        sut.Current.Should().Be(new AppSettings());
    }

    // ---- robustness ----

    [Fact]
    public void MissingKeys_FromOlderFile_FallBackToDefaults()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(SettingsFile, "{ \"SchemaVersion\": 1, \"Pet\": { \"Size\": \"Small\" } }");

        var s = Create().Current;

        s.Pet.Size.Should().Be(PetSize.Small);
        s.Behavior.SleepDelaySeconds.Should().Be(300);
    }

    [Fact]
    public void UnknownKeys_FromNewerFile_AreIgnored()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(SettingsFile, "{ \"SchemaVersion\": 1, \"Future\": { \"X\": 1 }, \"Pet\": { \"Size\": \"Large\" } }");

        Create().Current.Pet.Size.Should().Be(PetSize.Large);
    }

    [Fact]
    public void CorruptFile_IsSetAside_AndDefaultsAreUsed()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(SettingsFile, "{ this is not json");

        var s = Create().Current;

        s.Should().Be(new AppSettings());
        Directory.GetFiles(_root, "settings.json.corrupt-*").Should().HaveCount(1);
    }

    [Fact]
    public void NewerSchemaVersion_IsBackedUpBeforeAnyOverwrite()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(SettingsFile, "{ \"SchemaVersion\": 9, \"Pet\": { \"Size\": \"Large\" } }");

        Create();

        File.Exists(SettingsFile + ".v9.bak").Should().BeTrue();
    }

    [Fact]
    public void OutOfRangeValues_AreClamped()
    {
        Directory.CreateDirectory(_root);
        File.WriteAllText(SettingsFile, "{ \"Pet\": { \"AnimationSpeed\": 99 }, \"Behavior\": { \"SleepDelaySeconds\": 1 } }");

        var s = Create().Current;

        s.Pet.AnimationSpeed.Should().Be(SettingsLimits.MaxAnimationSpeed);
        s.Behavior.SleepDelaySeconds.Should().Be(SettingsLimits.MinSleepDelaySeconds);
    }

    // ---- migration ----

    [Fact]
    public void Migrator_RunsEachStepInOrder_AndStampsTargetVersion()
    {
        var root = JsonNode.Parse("{ \"SchemaVersion\": 1 }")!.AsObject();
        var order = new List<int>();
        var steps = new Dictionary<int, Action<JsonObject>>
        {
            [1] = o => { order.Add(1); o["A"] = true; },
            [2] = o => { order.Add(2); o["B"] = true; }
        };

        var changed = SettingsMigrator.Migrate(root, 3, steps);

        changed.Should().BeTrue();
        order.Should().Equal(1, 2);
        root["SchemaVersion"]!.GetValue<int>().Should().Be(3);
    }

    [Fact]
    public void Migrator_DoesNothingWhenAlreadyCurrent()
    {
        var root = JsonNode.Parse("{ \"SchemaVersion\": 2 }")!.AsObject();

        SettingsMigrator.Migrate(root, 2, new Dictionary<int, Action<JsonObject>>()).Should().BeFalse();
    }

    [Fact]
    public void Migrator_TreatsMissingVersionAsOne()
    {
        SettingsMigrator.ReadVersion(new JsonObject()).Should().Be(1);
    }

    // ---- quiet hours ----

    private static QuietHoursSettings Quiet(string start, string end, bool enabled = true) =>
        new() { Enabled = enabled, Start = TimeOnly.Parse(start), End = TimeOnly.Parse(end) };

    [Theory]
    [InlineData("22:00", "07:00", "23:30", true)]
    [InlineData("22:00", "07:00", "03:00", true)]
    [InlineData("22:00", "07:00", "07:00", false)]
    [InlineData("22:00", "07:00", "12:00", false)]
    [InlineData("12:00", "13:00", "12:30", true)]
    [InlineData("12:00", "13:00", "13:00", false)]
    [InlineData("08:00", "08:00", "08:00", false)]
    public void QuietHours_HandlesNormalAndMidnightRanges(string start, string end, string now, bool expected)
    {
        QuietHours.IsQuiet(Quiet(start, end), TimeOnly.Parse(now)).Should().Be(expected);
    }

    [Fact]
    public void QuietHours_WhenDisabled_IsNeverQuiet()
    {
        QuietHours.IsQuiet(Quiet("00:00", "23:59", enabled: false), new TimeOnly(12, 0)).Should().BeFalse();
    }
}
