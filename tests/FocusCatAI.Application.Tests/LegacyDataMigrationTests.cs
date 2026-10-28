using FluentAssertions;
using FocusCatAI.Infrastructure;

namespace FocusCatAI.Application.Tests;

public sealed class LegacyDataMigrationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "wb-migration-" + Guid.NewGuid().ToString("N"));
    private string Legacy => Path.Combine(_root, "FocusCatAI");
    private string Current => Path.Combine(_root, "WorkBuddy");

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, true);
        }
    }

    private void WriteLegacy(string relative, string content)
    {
        var path = Path.Combine(Legacy, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void CopiesPreferencesFromLegacyFolder()
    {
        WriteLegacy("pet-preferences.json", "{\"Introduced\":true}");

        var copied = LegacyDataMigration.Run(Legacy, Current);

        copied.Should().Be(1);
        File.ReadAllText(Path.Combine(Current, "pet-preferences.json")).Should().Be("{\"Introduced\":true}");
    }

    [Fact]
    public void LeavesLegacyFolderUntouched()
    {
        WriteLegacy("pet-preferences.json", "old");

        LegacyDataMigration.Run(Legacy, Current);

        File.Exists(Path.Combine(Legacy, "pet-preferences.json")).Should().BeTrue();
    }

    [Fact]
    public void DoesNotCopyLogs()
    {
        WriteLegacy(Path.Combine("logs", "focuscat-20260101.log"), "log");
        WriteLegacy("pet-preferences.json", "x");

        LegacyDataMigration.Run(Legacy, Current);

        Directory.Exists(Path.Combine(Current, "logs")).Should().BeFalse();
    }

    [Fact]
    public void NeverOverwritesExistingNewData()
    {
        WriteLegacy("pet-preferences.json", "old");
        Directory.CreateDirectory(Current);
        File.WriteAllText(Path.Combine(Current, "pet-preferences.json"), "new");

        LegacyDataMigration.Run(Legacy, Current);

        File.ReadAllText(Path.Combine(Current, "pet-preferences.json")).Should().Be("new");
    }

    [Fact]
    public void RunsOnlyOnce()
    {
        WriteLegacy("pet-preferences.json", "old");
        LegacyDataMigration.Run(Legacy, Current);
        File.Delete(Path.Combine(Current, "pet-preferences.json"));

        var second = LegacyDataMigration.Run(Legacy, Current);

        second.Should().Be(0);
        File.Exists(Path.Combine(Current, "pet-preferences.json")).Should().BeFalse();
    }

    [Fact]
    public void DoesNothingWhenNoLegacyFolderExists()
    {
        var copied = LegacyDataMigration.Run(Legacy, Current);

        copied.Should().Be(0);
        Directory.Exists(Current).Should().BeFalse();
    }
}
