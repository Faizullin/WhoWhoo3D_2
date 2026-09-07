using System.Collections.Generic;

namespace WhoWhoo3D.Domain.Wave;

public sealed class WaveStep
{
    public List<SlotStateChange> Changes { get; } = new();
}
