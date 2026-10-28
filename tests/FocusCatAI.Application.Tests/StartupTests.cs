using FluentAssertions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Infrastructure.Startup;
using FocusCatAI.Shared;
using Microsoft.Extensions.Logging.Abstractions;

namespace FocusCatAI.Application.Tests;

public sealed class StartupTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "wb-startup-" + Guid.NewGuid().ToString("N"));
    private readonly FakeRunKeyStore _store = new();

    public StartupTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string CreateExe(string folder, string name = "WorkBuddy.exe")
    {
        var path = Path.Combine(_dir, folder);
        Directory.CreateDirectory(path);
        var file = Path.Combine(path, name);
        File.WriteAllText(file, "x");
        return file;
    }

    private StartupManager Manager(string exe) => new(_store, exe, NullLogger<StartupManager>.Instance);

    [Fact]
    public void NothingRegisteredReportsNotRegistered()
    {
        Manager(CreateExe("a")).GetStatus().State.Should().Be(StartupState.NotRegistered);
    }

    [Fact]
    public void EnableWritesQuotedPathWithAutostartArgument()
    {
        var exe = CreateExe("a");
        Manager(exe).Enable();
        _store.Values[AppConstants.StartupValueName].Should().Be($"\"{exe}\" --autostart");
    }

    [Fact]
    public void EnableReportsRegistered()
    {
        Manager(CreateExe("a")).Enable().State.Should().Be(StartupState.Registered);
    }

    [Fact]
    public void EnableHandlesPathsWithSpaces()
    {
        var exe = CreateExe("Program Files\\Work Buddy");
        var manager = Manager(exe);
        manager.Enable();
        manager.GetStatus().State.Should().Be(StartupState.Registered);
    }

    [Fact]
    public void DisableRemovesTheEntry()
    {
        var manager = Manager(CreateExe("a"));
        manager.Enable();
        manager.Disable().State.Should().Be(StartupState.NotRegistered);
    }

    [Fact]
    public void DisableWhenNotRegisteredIsHarmless()
    {
        Manager(CreateExe("a")).Disable().State.Should().Be(StartupState.NotRegistered);
    }

    [Fact]
    public void MissingExecutableReportsInvalidPath()
    {
        var exe = CreateExe("a");
        _store.Values[AppConstants.StartupValueName] = $"\"{Path.Combine(_dir, "gone", "WorkBuddy.exe")}\" --autostart";
        Manager(exe).GetStatus().State.Should().Be(StartupState.InvalidPath);
    }

    [Fact]
    public void OtherExistingExecutableReportsPathMismatch()
    {
        var current = CreateExe("new");
        var old = CreateExe("old");
        _store.Values[AppConstants.StartupValueName] = $"\"{old}\" --autostart";
        Manager(current).GetStatus().State.Should().Be(StartupState.PathMismatch);
    }

    [Fact]
    public void MissingAutostartArgumentReportsPathMismatch()
    {
        var exe = CreateExe("a");
        _store.Values[AppConstants.StartupValueName] = $"\"{exe}\"";
        Manager(exe).GetStatus().State.Should().Be(StartupState.PathMismatch);
    }

    [Fact]
    public void DisabledInWindowsIsReportedHonestly()
    {
        var manager = Manager(CreateExe("a"));
        manager.Enable();
        _store.DisabledByWindows = true;
        manager.GetStatus().State.Should().Be(StartupState.DisabledByWindows);
    }

    [Fact]
    public void DisabledInWindowsDoesNotStartAtSignIn()
    {
        var manager = Manager(CreateExe("a"));
        manager.Enable();
        _store.DisabledByWindows = true;
        manager.GetStatus().WillStartAtSignIn.Should().BeFalse();
    }

    [Fact]
    public void EnableClearsTheWindowsDisabledFlag()
    {
        var manager = Manager(CreateExe("a"));
        _store.DisabledByWindows = true;
        manager.Enable().State.Should().Be(StartupState.Registered);
    }

    [Fact]
    public void SynchronizeWantedRegistersWhenMissing()
    {
        Manager(CreateExe("a")).Synchronize(true).State.Should().Be(StartupState.Registered);
    }

    [Fact]
    public void SynchronizeNotWantedRemovesTheEntry()
    {
        var manager = Manager(CreateExe("a"));
        manager.Enable();
        manager.Synchronize(false).State.Should().Be(StartupState.NotRegistered);
    }

    [Fact]
    public void SynchronizeRepairsAMovedInstall()
    {
        var old = CreateExe("old");
        var current = CreateExe("new");
        _store.Values[AppConstants.StartupValueName] = $"\"{old}\" --autostart";
        Manager(current).Synchronize(true).State.Should().Be(StartupState.Registered);
    }

    [Fact]
    public void SynchronizeRewritesTheCommandToTheNewPath()
    {
        var old = CreateExe("old");
        var current = CreateExe("new");
        _store.Values[AppConstants.StartupValueName] = $"\"{old}\" --autostart";
        Manager(current).Synchronize(true);
        _store.Values[AppConstants.StartupValueName].Should().Be($"\"{current}\" --autostart");
    }

    [Fact]
    public void SynchronizeRepairsADeletedInstall()
    {
        var current = CreateExe("new");
        _store.Values[AppConstants.StartupValueName] = $"\"{Path.Combine(_dir, "deleted", "WorkBuddy.exe")}\" --autostart";
        Manager(current).Synchronize(true).State.Should().Be(StartupState.Registered);
    }

    [Fact]
    public void SynchronizeDoesNotOverrideAUserDisablingItInWindows()
    {
        var manager = Manager(CreateExe("a"));
        manager.Enable();
        _store.DisabledByWindows = true;
        manager.Synchronize(true).State.Should().Be(StartupState.DisabledByWindows);
    }

    [Fact]
    public void SynchronizeNotWantedWhenNotRegisteredWritesNothing()
    {
        Manager(CreateExe("a")).Synchronize(false);
        _store.Values.Should().BeEmpty();
    }

    [Theory]
    [InlineData("\"C:\\A B\\WorkBuddy.exe\" --autostart", "C:\\A B\\WorkBuddy.exe")]
    [InlineData("C:\\Apps\\WorkBuddy.exe --autostart", "C:\\Apps\\WorkBuddy.exe")]
    [InlineData("C:\\Apps\\WorkBuddy.exe", "C:\\Apps\\WorkBuddy.exe")]
    public void ParseExecutableHandlesQuotedAndUnquotedCommands(string command, string expected)
    {
        StartupManager.ParseExecutable(command).Should().Be(expected);
    }

    [Fact]
    public void ParseExecutableOfEmptyCommandIsNull()
    {
        StartupManager.ParseExecutable("  ").Should().BeNull();
    }

    [Fact]
    public void AutostartArgumentIsRecognised()
    {
        StartupOptions.Parse(["--autostart"]).Autostart.Should().BeTrue();
    }

    [Fact]
    public void NoArgumentsIsANormalLaunch()
    {
        StartupOptions.Parse([]).Should().Be(new StartupOptions(false, false, false));
    }

    [Fact]
    public void AutostartNeverRunsDiagnostics()
    {
        var options = StartupOptions.Parse(["--autostart", "--verify-settings", "--verify-startup"]);
        (options.VerifySettings || options.VerifyStartup).Should().BeFalse();
    }

    [Fact]
    public void VerifyStartupArgumentIsRecognised()
    {
        StartupOptions.Parse(["--VERIFY-STARTUP"]).VerifyStartup.Should().BeTrue();
    }

    private sealed class FakeRunKeyStore : IRunKeyStore
    {
        public Dictionary<string, string> Values { get; } = new();
        public bool DisabledByWindows { get; set; }

        public string? GetCommand(string valueName) => Values.GetValueOrDefault(valueName);
        public void SetCommand(string valueName, string command) => Values[valueName] = command;
        public void RemoveCommand(string valueName) => Values.Remove(valueName);
        public bool IsDisabledByWindows(string valueName) => DisabledByWindows;
        public void ClearDisabledByWindows(string valueName) => DisabledByWindows = false;
    }
}
