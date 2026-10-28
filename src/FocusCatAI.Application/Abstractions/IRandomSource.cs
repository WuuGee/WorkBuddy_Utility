namespace FocusCatAI.Application.Abstractions;

public interface IRandomSource
{
    /// <summary>Returns an integer in [minInclusive, maxExclusive).</summary>
    int Next(int minInclusive, int maxExclusive);

    /// <summary>Returns a double in [0, 1).</summary>
    double NextDouble();
}
