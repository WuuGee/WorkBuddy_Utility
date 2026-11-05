using FluentAssertions;
using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Progression;
using FocusCatAI.Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace FocusCatAI.Application.Tests.Progression;

public class JsonProgressionStoreTests
{
    private static (JsonProgressionStore Store, string Dir) Create()
    {
        var dir = Path.Combine(Path.GetTempPath(), "wb-prog-" + Guid.NewGuid().ToString("N"));
        var paths = Substitute.For<IAppPaths>();
        paths.RootDirectory.Returns(dir);
        return (new JsonProgressionStore(paths, NullLogger<JsonProgressionStore>.Instance), dir);
    }

    [Fact]
    public void MissingFileLoadsDefaults()
    {
        var (store, _) = Create();
        store.Load().Should().Be(new ProgressionState());
    }

    [Fact]
    public void SavedStateRoundTrips()
    {
        var (store, _) = Create();
        var state = new ProgressionState
        {
            TotalXp = 123,
            TotalCompanionshipMinutes = 99,
            LastInteractionUtc = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
            InteractionDateUtc = new DateOnly(2026, 1, 10),
            DailyInteractionXp = 8
        };
        store.Save(state);
        store.Load().Should().Be(state);
    }

    [Fact]
    public void SaveWritesProgressionJson()
    {
        var (store, dir) = Create();
        store.Save(new ProgressionState());
        File.Exists(Path.Combine(dir, "progression.json")).Should().BeTrue();
    }

    [Fact]
    public void SecondSaveReplacesFirst()
    {
        var (store, _) = Create();
        store.Save(new ProgressionState { TotalXp = 1 });
        store.Save(new ProgressionState { TotalXp = 2 });
        store.Load().TotalXp.Should().Be(2);
    }

    [Fact]
    public void CorruptFileLoadsDefaults()
    {
        var (store, dir) = Create();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "progression.json"), "{ not json");
        store.Load().Should().Be(new ProgressionState());
    }

    [Fact]
    public void CorruptFileIsSetAside()
    {
        var (store, dir) = Create();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "progression.json"), "{ not json");
        store.Load();
        Directory.GetFiles(dir, "progression.json.corrupt-*").Should().HaveCount(1);
    }

    [Fact]
    public void NegativeValuesAreClamped()
    {
        var (store, dir) = Create();
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "progression.json"), "{\"TotalXp\": -5, \"TotalCompanionshipMinutes\": -1, \"DailyInteractionXp\": 999}");
        var s = store.Load();
        (s.TotalXp, s.TotalCompanionshipMinutes, s.DailyInteractionXp).Should().Be((0L, 0L, 30));
    }
}
