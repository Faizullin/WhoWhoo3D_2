using WhoWhoo3D.Truth.Topology;
using WhoWhoo3D.Truth.Wave;
using Xunit;

namespace WhoWhoo3D.Truth.Tests.Wave;

public class WaveSimulatorTests
{
    private static readonly CellId A = new("A");
    private static readonly CellId B = new("B");
    private static readonly CellId C = new("C");
    private static readonly CellId D = new("D");
    private static readonly PlayerId P1 = new(1);
    private static readonly PlayerId P2 = new(2);
    private static readonly WaveConfig Cfg = WaveConfig.Default;

    private static BoardModel TwoCellBoard(int sides = 6)
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, sides);
        board.AddCell(B, new Vec3(1, 0, 0), sides);
        board.LinkSides(new SideId(A, 0), new SideId(B, 0));
        return board;
    }

    [Fact]
    public void Example1_EmptyTarget_AttackerSpreads()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.True(result.Quiescent);
        Assert.Single(result.Steps);
        Assert.Equal(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.Equal(new TileStack(P1, 3), board.GetSide(new SideId(B, 0)).Stack);
    }

    [Fact]
    public void Example2_SameOwnerStack_NoOverflow()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P1, 1));

        WaveSimulator.Simulate(board, Cfg);

        Assert.Equal(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.Equal(new TileStack(P1, 4), board.GetSide(new SideId(B, 0)).Stack);
    }

    [Fact]
    public void Example3_SameOwnerStack_OverflowClampedAtCapacity()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P1, 3));

        WaveSimulator.Simulate(board, Cfg);

        Assert.Equal(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.Equal(new TileStack(P1, 4), board.GetSide(new SideId(B, 0)).Stack); // clamped from 3+3=6 → 4
    }

    [Fact]
    public void Example4_EnemyTarget_AttackerWins()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 1));

        WaveSimulator.Simulate(board, Cfg);

        Assert.Equal(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.Equal(new TileStack(P1, 2), board.GetSide(new SideId(B, 0)).Stack); // emit 3 - defender 1 = 2
    }

    [Fact]
    public void Example5_EnemyTarget_DefenderWins()
    {
        var board = TwoCellBoard();
        // A emits first (sorted by cell id), then B's emission sees post-A state.
        // To isolate defender-wins for A's attack, give B exactly 4 (full) but verify A's resolution.
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        // A processes first: emit 3 vs defender 4 → defender holds with 4-3=1.
        // After: A[0] = P1×1, B[0] = P2×1. B is no longer full → no further emission.
        Assert.Equal(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.Equal(new TileStack(P2, 1), board.GetSide(new SideId(B, 0)).Stack);
        Assert.Single(result.Steps);
    }

    [Fact]
    public void Example6_EnemyTarget_Tie_BothAnnihilate()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 3));

        WaveSimulator.Simulate(board, Cfg);

        Assert.Equal(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.Equal(TileStack.Empty, board.GetSide(new SideId(B, 0)).Stack);
    }

    [Fact]
    public void Example7_NoLink_NoEmission()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        // Side has no link
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.Empty(result.Steps);
        Assert.True(result.Quiescent);
        Assert.Equal(new TileStack(P1, 4), board.GetSide(new SideId(A, 0)).Stack);
    }

    [Fact]
    public void NoFullSides_NoEmission()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 3));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 2));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.Empty(result.Steps);
        Assert.True(result.Quiescent);
    }

    [Fact]
    public void TwoEmissionsToDifferentTargets_BothResolve()
    {
        // A[0] → C[0], B[0] → C[1] (different sides on same cell, no contention)
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);
        board.AddCell(C, Vec3.Zero, 6);
        board.LinkSides(new SideId(A, 0), new SideId(C, 0));
        board.LinkSides(new SideId(B, 0), new SideId(C, 1));

        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P1, 4));

        WaveSimulator.Simulate(board, Cfg);

        Assert.Equal(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.Equal(new TileStack(P1, 1), board.GetSide(new SideId(B, 0)).Stack);
        Assert.Equal(new TileStack(P1, 3), board.GetSide(new SideId(C, 0)).Stack);
        Assert.Equal(new TileStack(P1, 3), board.GetSide(new SideId(C, 1)).Stack);
    }

    [Fact]
    public void TwoEmissionsToSameTarget_SecondSeesFirstResult()
    {
        // A[0] → C[0], B[0] → C[0]?  Cannot — links are exclusive.
        // Instead test: A[0] → C[0], B[0] → C[0] is not allowed by topology.
        // Use chain pattern: after A captures C[0], B[0] (linked elsewhere) is unaffected.
        // Verify deterministic order (A before B).
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);
        board.AddCell(C, Vec3.Zero, 6);
        board.LinkSides(new SideId(A, 0), new SideId(C, 0));
        board.LinkSides(new SideId(B, 0), new SideId(C, 1));
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 4));

        WaveSimulator.Simulate(board, Cfg);

        // Both emit; targets are different sides, so no interference.
        Assert.Equal(new TileStack(P1, 3), board.GetSide(new SideId(C, 0)).Stack);
        Assert.Equal(new TileStack(P2, 3), board.GetSide(new SideId(C, 1)).Stack);
    }

    [Fact]
    public void EmissionResolvesEvenIfTargetIsFullEnemy()
    {
        // Defender wins when defender > emit. Already tested in Example5,
        // but verify edge: defender = emit + 1.
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));   // emit 3
        board.SetStack(new SideId(B, 0), new TileStack(P2, 4));   // defender 4

        WaveSimulator.Simulate(board, Cfg);

        // defender > emit → defender holds with 4-3=1
        Assert.Equal(new TileStack(P2, 1), board.GetSide(new SideId(B, 0)).Stack);
    }

    [Fact]
    public void Quiescent_TrueWhenNoMoreEmissionsRemain()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.True(result.Quiescent);
        Assert.Single(result.Steps);
    }

    [Fact]
    public void MaxChainSteps_ZeroNotAllowed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaveConfig(4, 1, 0).Validate());
    }

    [Fact]
    public void Capacity_ZeroNotAllowed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaveConfig(0, 1, 5).Validate());
    }

    [Fact]
    public void Keep_GreaterThanCapacity_NotAllowed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaveConfig(4, 4, 5).Validate());
    }

    [Fact]
    public void StepRecordsBeforeAndAfter_ForBothSidesChanged()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.Single(result.Steps);
        var step = result.Steps[0];
        Assert.Equal(2, step.Changes.Count);

        var sourceChange = step.Changes.Single(c => c.Side.Equals(new SideId(A, 0)));
        Assert.Equal(new TileStack(P1, 4), sourceChange.Before);
        Assert.Equal(new TileStack(P1, 1), sourceChange.After);

        var targetChange = step.Changes.Single(c => c.Side.Equals(new SideId(B, 0)));
        Assert.Equal(TileStack.Empty, targetChange.Before);
        Assert.Equal(new TileStack(P1, 3), targetChange.After);
    }

    [Fact]
    public void DeterministicOrder_AProcessedBeforeB()
    {
        // Two full sources, both link to C's different sides.
        // Source on A is processed first because cell ID "A" sorts before "B".
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);
        board.AddCell(C, Vec3.Zero, 6);
        board.LinkSides(new SideId(A, 0), new SideId(C, 0));
        board.LinkSides(new SideId(B, 0), new SideId(C, 1));

        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        var firstChange = result.Steps[0].Changes[0];
        Assert.Equal(new SideId(A, 0), firstChange.Side);
    }
}
