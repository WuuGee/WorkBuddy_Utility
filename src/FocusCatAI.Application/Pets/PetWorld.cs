using FocusCatAI.Application.Abstractions;
using FocusCatAI.Domain.Taskbar;

namespace FocusCatAI.Application.Pets;

public sealed class PetWorld : IPetWorld
{
    public double PetX { get; set; }
    public double PetWidth { get; set; } = 64;
    public double PetHeight { get; set; } = 64;
    public double PetTop { get; set; }
    public double WalkSpeed { get; set; } = 40;

    public TaskbarLayout Layout { get; set; } =
        new(TaskbarPlacementMode.AboveTaskbar, 0, 0, 0, 0, Array.Empty<TaskbarElement>());

    public double? CursorX { get; set; }
}
