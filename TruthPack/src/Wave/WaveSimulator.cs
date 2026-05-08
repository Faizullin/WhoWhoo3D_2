using WhoWhoo3D.Truth.Topology;

namespace WhoWhoo3D.Truth.Wave;

public static class WaveSimulator
{
    public static WaveResult Simulate(BoardModel board, WaveConfig cfg)
    {
        cfg.Validate();
        var steps = new List<WaveStep>();
        bool quiescent = true;

        for (int i = 0; i < cfg.MaxChainSteps; i++)
        {
            var emissions = FindEmissions(board, cfg);
            if (emissions.Count == 0)
            {
                quiescent = true;
                break;
            }

            quiescent = false;
            var step = new WaveStep();

            foreach (var sourceId in emissions)
                ResolveEmission(board, sourceId, cfg, step);

            steps.Add(step);

            if (FindEmissions(board, cfg).Count == 0)
            {
                quiescent = true;
                break;
            }
        }

        return new WaveResult(steps, quiescent);
    }

    private static List<SideId> FindEmissions(BoardModel board, WaveConfig cfg)
    {
        var result = new List<SideId>();
        foreach (var (_, cell) in board.Cells.OrderBy(kv => kv.Key.Value, StringComparer.Ordinal))
        {
            for (int i = 0; i < cell.Sides.Count; i++)
            {
                var side = cell.Sides[i];
                if (side.Stack.Count >= cfg.Capacity && side.Link.HasValue)
                    result.Add(side.Id);
            }
        }
        return result;
    }

    private static void ResolveEmission(BoardModel board, SideId sourceId, WaveConfig cfg, WaveStep step)
    {
        var source = board.GetSide(sourceId);

        // Earlier emission this step may have changed the source.
        if (source.Stack.Count < cfg.Capacity || !source.Link.HasValue)
            return;

        var sourceBefore = source.Stack;
        var targetId = source.Link.Value.Other;
        var target = board.GetSide(targetId);
        var targetBefore = target.Stack;

        var attacker = sourceBefore.Owner;
        var emitCount = sourceBefore.Count - cfg.Keep;

        TileStack newSource = new(attacker, cfg.Keep);
        TileStack newTarget;

        if (targetBefore.IsEmpty)
        {
            newTarget = new TileStack(attacker, emitCount);
        }
        else if (targetBefore.Owner.Equals(attacker))
        {
            var combined = Math.Min(targetBefore.Count + emitCount, cfg.Capacity);
            newTarget = new TileStack(attacker, combined);
        }
        else if (emitCount > targetBefore.Count)
        {
            newTarget = new TileStack(attacker, emitCount - targetBefore.Count);
        }
        else if (emitCount < targetBefore.Count)
        {
            newTarget = new TileStack(targetBefore.Owner, targetBefore.Count - emitCount);
        }
        else
        {
            newTarget = TileStack.Empty;
        }

        board.SetStack(sourceId, newSource);
        board.SetStack(targetId, newTarget);

        if (!sourceBefore.Equals(newSource))
            step.Changes.Add(new SideStateChange(sourceId, sourceBefore, newSource));
        if (!targetBefore.Equals(newTarget))
            step.Changes.Add(new SideStateChange(targetId, targetBefore, newTarget));
    }
}
