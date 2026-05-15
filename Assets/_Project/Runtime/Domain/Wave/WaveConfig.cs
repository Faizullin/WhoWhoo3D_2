using System;

namespace WhoWhoo3D.Domain.Wave;

public sealed record WaveConfig(int Capacity, int Keep, int MaxChainSteps)
{
    public static WaveConfig Default => new(Capacity: 4, Keep: 1, MaxChainSteps: 5);

    public void Validate()
    {
        if (Capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(Capacity), "Capacity must be positive.");
        if (Keep < 0)
            throw new ArgumentOutOfRangeException(nameof(Keep), "Keep must be non-negative.");
        if (Keep >= Capacity)
            throw new ArgumentOutOfRangeException(nameof(Keep), "Keep must be less than Capacity.");
        if (MaxChainSteps <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxChainSteps), "MaxChainSteps must be positive.");
    }
}
