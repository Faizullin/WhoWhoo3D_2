using System.Collections.Generic;
using System.Linq;

namespace WhoWhoo3D.Domain.Wave;

public sealed class WaveResult
{
    public IReadOnlyList<WaveStep> Steps { get; }

    /// <summary>True when no face was left able to expand.</summary>
    public bool Quiescent { get; }

    /// <summary>
    /// True when the chain stopped because at most one player still holds tiles. The game
    /// is over at that point, so simulating further steps would only churn a board nobody
    /// is going to play on.
    /// </summary>
    public bool Decided { get; }

    public WaveResult(IReadOnlyList<WaveStep> steps, bool quiescent, bool decided = false)
    {
        Steps = steps;
        Quiescent = quiescent;
        Decided = decided;
    }

    public int TotalChanges => Steps.Sum(s => s.Changes.Count);
}
