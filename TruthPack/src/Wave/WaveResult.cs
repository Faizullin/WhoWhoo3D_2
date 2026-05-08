namespace WhoWhoo3D.Truth.Wave;

public sealed class WaveResult
{
    public IReadOnlyList<WaveStep> Steps { get; }
    public bool Quiescent { get; }

    public WaveResult(IReadOnlyList<WaveStep> steps, bool quiescent)
    {
        Steps = steps;
        Quiescent = quiescent;
    }

    public int TotalChanges => Steps.Sum(s => s.Changes.Count);
}
