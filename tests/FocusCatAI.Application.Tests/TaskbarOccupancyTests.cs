using FluentAssertions;
using FocusCatAI.Application.Taskbar;
using FocusCatAI.Domain.Taskbar;

namespace FocusCatAI.Application.Tests;

public class TaskbarOccupancyTests
{
    private static TaskbarLayout Bar(double height) =>
        new(TaskbarPlacementMode.InTaskbar, 0, 1000, 1000, 1000 + height, Array.Empty<TaskbarElement>());

    [Theory]
    [InlineData(32, 32)]
    [InlineData(32, 48)]
    [InlineData(32, 64)]
    [InlineData(32, 96)]
    [InlineData(48, 32)]
    [InlineData(48, 48)]
    [InlineData(48, 64)]
    [InlineData(48, 96)]
    [InlineData(64, 32)]
    [InlineData(64, 48)]
    [InlineData(64, 64)]
    [InlineData(64, 96)]
    public void PetBounds_AreAlwaysInsideTaskbarBounds(double taskbarHeight, double petSize)
    {
        var bar = Bar(taskbarHeight);

        var bounds = TaskbarFit.ComputeBounds(bar, petSize, 100);

        (bounds.Top >= bar.Top && bounds.Bottom <= bar.Bottom).Should().BeTrue();
    }

    [Fact]
    public void PetBottom_EqualsTaskbarBottom()
    {
        var bar = Bar(48);

        TaskbarFit.ComputeBounds(bar, 96, 0).Bottom.Should().Be(bar.Bottom);
    }

    [Fact]
    public void LargePet_IsScaledDownOnA48pxTaskbar()
    {
        TaskbarFit.ComputeSize(Bar(48), 64).Should().BeLessThan(64);
    }

    [Fact]
    public void SmallPet_IsNotScaledOnA48pxTaskbar()
    {
        TaskbarFit.ComputeSize(Bar(48), 32).Should().Be(32);
    }
}
