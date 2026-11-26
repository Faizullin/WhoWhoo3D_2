using System.Collections.Generic;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Gameplay.Board;

namespace WhoWhoo3D.Gameplay.Ai;

/// <summary>
/// Ranks each legal claim by net tile gain after expansion. Ties go to the lowest slot ID.
/// </summary>
public sealed class GreedyAiPlayer
{
    private readonly WaveConfig _config;

    public GreedyAiPlayer(WaveConfig config)
    {
        _config = config;
    }

    public readonly struct MoveEvaluation
    {
        public SlotId Slot { get; }
        public int ScoreDelta { get; }

        public MoveEvaluation(SlotId slot, int scoreDelta)
        {
            Slot = slot;
            ScoreDelta = scoreDelta;
        }

        public override string ToString() => $"{Slot}: {ScoreDelta:+0;-0;0}";
    }

    public bool TryChooseMove(BoardModel board, PlayerId me, out SlotId chosen)
    {
        chosen = default;

        var evaluations = EvaluateAll(board, me);
        if (evaluations.Count == 0)
            return false;

        var best = evaluations[0];
        for (int i = 1; i < evaluations.Count; i++)
            if (evaluations[i].ScoreDelta > best.ScoreDelta)
                best = evaluations[i];

        chosen = best.Slot;
        return true;
    }

    /// <summary>Scores every legal move, in ascending slot order. Exposed for tests and debug overlays.</summary>
    public List<MoveEvaluation> EvaluateAll(BoardModel board, PlayerId me)
    {
        var results = new List<MoveEvaluation>();

        // LegalMoves is already sorted, so the strictly-greater comparison in
        // TryChooseMove breaks ties toward the lowest slot without a second sort.
        var moves = BoardActions.LegalMoves(board, me);
        if (moves.Count == 0)
            return results;

        int myBefore = BoardActions.ScoreFor(board, me);
        int opponentsBefore = BoardActions.OpponentScore(board, me);

        foreach (var slot in moves)
            results.Add(new MoveEvaluation(slot, Evaluate(board, me, slot, myBefore, opponentsBefore)));

        return results;
    }

    private int Evaluate(BoardModel board, PlayerId me, SlotId slotId, int myBefore, int opponentsBefore)
    {
        // Non-destructive: the live board must not move while we are thinking.
        var hypothetical = board.Clone();

        if (!BoardActions.CanClaim(hypothetical, slotId, me))
            return int.MinValue;

        hypothetical.SetOwner(slotId, me);
        WaveSimulator.Simulate(hypothetical, _config);

        int myAfter = BoardActions.ScoreFor(hypothetical, me);
        int opponentsAfter = BoardActions.OpponentScore(hypothetical, me);

        return (myAfter - myBefore) - (opponentsAfter - opponentsBefore);
    }
}

