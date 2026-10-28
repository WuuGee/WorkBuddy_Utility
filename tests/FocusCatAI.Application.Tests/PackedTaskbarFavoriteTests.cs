using FluentAssertions;
using FocusCatAI.Application.Taskbar;
using FocusCatAI.Domain.Taskbar;

namespace FocusCatAI.Application.Tests;

/// <summary>Uses the real left-aligned, packed Windows 11 layout captured from a 2560px-wide taskbar.</summary>
public class PackedTaskbarFavoriteTests
{
    private static TaskbarElement App(string name, double left) => new(name, TaskbarElementKind.App, left, left + 44, true);

    private static readonly TaskbarLayout Layout = new(
        TaskbarPlacementMode.InTaskbar, 0, 1392, 2560, 1440,
        [
            new TaskbarElement("Start", TaskbarElementKind.Start, 0, 55, false),
            new TaskbarElement("Search", TaskbarElementKind.Search, 57, 277, false),
            new TaskbarElement("Task View", TaskbarElementKind.TaskView, 279, 323, false),
            App("File Explorer - 2 running windows pinned", 323),
            App("Google Chrome pinned", 367),
            App("Outlook - 1 running window pinned", 411),
            App("Microsoft Teams - 1 running window pinned", 455),
            App("Company Portal pinned", 499),
            App("Visual Studio Code - 1 running window", 763),
            App("Excel - 4 running windows", 807),
            App("Visual Studio 2022 - 4 running windows", 895),
            App("Notepad - 1 running window", 939),
            new TaskbarElement("Widgets", TaskbarElementKind.Widgets, 2106, 2258, false),
            new TaskbarElement("Notification area", TaskbarElementKind.Tray, 2270, 2560, false)
        ]);

    private static readonly IReadOnlyList<Zone> Zones = TaskbarGeometry.GetZones(Layout, 44);

    private static TaskbarGeometry.FavoriteResolution Resolve(string key) =>
        TaskbarGeometry.ResolveFavoriteDetailed(Layout, Zones, key, 44)!;

    [Fact]
    public void FavoriteDestinations_AreDistinct_OnAPackedTaskbar()
    {
        var destinations = new[] { "Start", "App:Microsoft Teams", "App:Visual Studio", "Clock" }
            .Select(k => Math.Round(Resolve(k).Destination)).ToList();

        destinations.Distinct().Should().HaveCount(4);
    }

    [Fact]
    public void Start_ResolvesToTheLeftEdgeOfTheFreeSpace()
    {
        var start = Resolve("Start");

        start.Zone.Should().Be("Start");
        start.Destination.Should().Be(Zones[0].Left);
    }

    [Fact]
    public void Clock_ResolvesToTheRightSide()
    {
        var clock = Resolve("Clock");

        clock.Zone.Should().Be("Clock");
        clock.Destination.Should().BeGreaterThan(1500);
    }

    [Fact]
    public void VisualStudio_DoesNotMatchVisualStudioCode()
    {
        TaskbarGeometry.FindElement(Layout, "App:Visual Studio")!.Name.Should().Contain("2022");
    }

    [Fact]
    public void EnclosedApp_ExplainsWhyItUsesAZone()
    {
        Resolve("App:Microsoft Teams").Reason.Should().Contain("enclosed");
    }

    [Fact]
    public void AppAtEndOfGroup_ResolvesAdjacentToTheGroup()
    {
        var notepad = TaskbarGeometry.ResolveNear(Layout, Zones, Layout.Elements.Single(e => e.Name.StartsWith("Notepad")), "k", 44)!;

        notepad.Zone.Should().Be("Adjacent");
    }

    [Fact]
    public void Destinations_NeverOverlapAnyTaskbarButton()
    {
        foreach (var key in new[] { "Start", "App:Microsoft Teams", "App:Visual Studio", "App:Excel", "Clock" })
        {
            var x = Resolve(key).Destination;
            Layout.Elements.Any(e => x < e.Right - 1 && x + 44 > e.Left + 1).Should().BeFalse($"{key} -> {x}");
        }
    }
}
