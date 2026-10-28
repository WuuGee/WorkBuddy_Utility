using FocusCatAI.Application.Abstractions;

namespace FocusCatAI.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
