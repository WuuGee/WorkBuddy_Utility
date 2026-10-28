using FluentAssertions;
using FocusCatAI.Application.Taskbar;
using FocusCatAI.Domain.Taskbar;

namespace FocusCatAI.Application.Tests;

public class TaskbarGeometryTests
{
    private static TaskbarLayout Layout(params TaskbarElement[] elements) =>
        new(TaskbarPlacementMode.InTaskbar, 0, 1000, 1000, 1048, elements);

    private static TaskbarElement El(string name, TaskbarElementKind kind, double l, double r) => new(name, kind, l, r, false);

    [Fact]
    public void GetZones_ReturnsGapsBetweenElements_AndIgnoresSmallOnes()
    {
        var layout = Layout(
            El("Start", TaskbarElementKind.Start, 0, 48),
            El("VS", TaskbarElementKind.App, 100, 148),
            El("Tray", TaskbarElementKind.Tray, 900, 1000));

        var zones = TaskbarGeometry.GetZones(layout, 40);

        zones.Should().Equal(new Zone(48, 100), new Zone(148, 900));
    }

    [Fact]
    public void GetZones_MergesOverlappingElements()
    {
        var layout = Layout(El("A", TaskbarElementKind.App, 100, 200), El("B", TaskbarElementKind.App, 150, 300));
        TaskbarGeometry.GetZones(layout, 40).Should().Equal(new Zone(0, 100), new Zone(300, 1000));
    }

    [Fact]
    public void ClampToNearestZone_PushesPetOutOfElement()
    {
        var zones = new[] { new Zone(0, 100), new Zone(300, 900) };
        TaskbarGeometry.ClampToNearestZone(zones, 250, 64).Should().Be(300);
        TaskbarGeometry.ClampToNearestZone(zones, 80, 64).Should().Be(36);
    }

    [Fact]
    public void SpotBeside_PicksClosestSideInsideAZone()
    {
        var teams = El("Teams - 1 running window", TaskbarElementKind.App, 400, 448);
        var layout = Layout(teams);
        var zones = TaskbarGeometry.GetZones(layout, 40);

        var spot = TaskbarGeometry.SpotBeside(zones, teams, 64);

        spot.Should().NotBeNull();
        (spot!.Value + 64 <= 400 || spot.Value >= 448).Should().BeTrue();
    }

    [Fact]
    public void FindElement_MatchesAppByNamePart_AndClockFallsBackToTray()
    {
        var layout = Layout(
            El("Microsoft Visual Studio - 1 running window", TaskbarElementKind.App, 100, 148),
            El("Notification area", TaskbarElementKind.Tray, 900, 1000));

        TaskbarGeometry.FindElement(layout, "App:Visual Studio").Should().NotBeNull();
        TaskbarGeometry.FindElement(layout, "Clock")!.Kind.Should().Be(TaskbarElementKind.Tray);
        TaskbarGeometry.FindElement(layout, "App:Teams").Should().BeNull();
    }
}
