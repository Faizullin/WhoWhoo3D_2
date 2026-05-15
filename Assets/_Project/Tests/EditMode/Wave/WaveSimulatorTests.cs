using NUnit.Framework;
using System;
using System.Linq;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;

namespace WhoWhoo3D.Tests.EditMode.Wave;

public class WaveSimulatorTests
{
    private static readonly CellId A = new("A");
    private static readonly CellId B = new("B");
    private static readonly CellId C = new("C");
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

    [Test]
    public void Example1_EmptyTarget_AttackerSpreads()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.IsTrue(result.Quiescent);
        Assert.AreEqual(1, result.Steps.Count);
        Assert.AreEqual(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.AreEqual(new TileStack(P1, 3), board.GetSide(new SideId(B, 0)).Stack);
    }

    [Test]
    public void Example2_SameOwnerStack_NoOverflow()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P1, 1));

        WaveSimulator.Simulate(board, Cfg);

        Assert.AreEqual(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.AreEqual(new TileStack(P1, 4), board.GetSide(new SideId(B, 0)).Stack);
    }

    [Test]
    public void Example3_SameOwnerStack_OverflowClampedAtCapacity()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P1, 3));

        WaveSimulator.Simulate(board, Cfg);

        Assert.AreEqual(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.AreEqual(new TileStack(P1, 4), board.GetSide(new SideId(B, 0)).Stack);
    }

    [Test]
    public void Example4_EnemyTarget_AttackerWins()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 1));

        WaveSimulator.Simulate(board, Cfg);

        Assert.AreEqual(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.AreEqual(new TileStack(P1, 2), board.GetSide(new SideId(B, 0)).Stack);
    }

    [Test]
    public void Example5_EnemyTarget_DefenderWins()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.AreEqual(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.AreEqual(new TileStack(P2, 1), board.GetSide(new SideId(B, 0)).Stack);
        Assert.AreEqual(1, result.Steps.Count);
    }

    [Test]
    public void Example6_EnemyTarget_Tie_BothAnnihilate()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 3));

        WaveSimulator.Simulate(board, Cfg);

        Assert.AreEqual(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.AreEqual(TileStack.Empty, board.GetSide(new SideId(B, 0)).Stack);
    }

    [Test]
    public void Example7_NoLink_NoEmission()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.IsEmpty(result.Steps);
        Assert.IsTrue(result.Quiescent);
        Assert.AreEqual(new TileStack(P1, 4), board.GetSide(new SideId(A, 0)).Stack);
    }

    [Test]
    public void NoFullSides_NoEmission()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 3));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 2));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.IsEmpty(result.Steps);
        Assert.IsTrue(result.Quiescent);
    }

    [Test]
    public void TwoEmissionsToDifferentTargets_BothResolve()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);
        board.AddCell(C, Vec3.Zero, 6);
        board.LinkSides(new SideId(A, 0), new SideId(C, 0));
        board.LinkSides(new SideId(B, 0), new SideId(C, 1));

        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P1, 4));

        WaveSimulator.Simulate(board, Cfg);

        Assert.AreEqual(new TileStack(P1, 1), board.GetSide(new SideId(A, 0)).Stack);
        Assert.AreEqual(new TileStack(P1, 1), board.GetSide(new SideId(B, 0)).Stack);
        Assert.AreEqual(new TileStack(P1, 3), board.GetSide(new SideId(C, 0)).Stack);
        Assert.AreEqual(new TileStack(P1, 3), board.GetSide(new SideId(C, 1)).Stack);
    }

    [Test]
    public void TwoEmissionsToSameTarget_SecondSeesFirstResult()
    {
        var board = new BoardModel();
        board.AddCell(A, Vec3.Zero, 6);
        board.AddCell(B, Vec3.Zero, 6);
        board.AddCell(C, Vec3.Zero, 6);
        board.LinkSides(new SideId(A, 0), new SideId(C, 0));
        board.LinkSides(new SideId(B, 0), new SideId(C, 1));
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 4));

        WaveSimulator.Simulate(board, Cfg);

        Assert.AreEqual(new TileStack(P1, 3), board.GetSide(new SideId(C, 0)).Stack);
        Assert.AreEqual(new TileStack(P2, 3), board.GetSide(new SideId(C, 1)).Stack);
    }

    [Test]
    public void EmissionResolvesEvenIfTargetIsFullEnemy()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));
        board.SetStack(new SideId(B, 0), new TileStack(P2, 4));

        WaveSimulator.Simulate(board, Cfg);

        Assert.AreEqual(new TileStack(P2, 1), board.GetSide(new SideId(B, 0)).Stack);
    }

    [Test]
    public void Quiescent_TrueWhenNoMoreEmissionsRemain()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.IsTrue(result.Quiescent);
        Assert.AreEqual(1, result.Steps.Count);
    }

    [Test]
    public void MaxChainSteps_ZeroNotAllowed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaveConfig(4, 1, 0).Validate());
    }

    [Test]
    public void Capacity_ZeroNotAllowed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaveConfig(0, 1, 5).Validate());
    }

    [Test]
    public void Keep_GreaterThanCapacity_NotAllowed()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WaveConfig(4, 4, 5).Validate());
    }

    [Test]
    public void StepRecordsBeforeAndAfter_ForBothSidesChanged()
    {
        var board = TwoCellBoard();
        board.SetStack(new SideId(A, 0), new TileStack(P1, 4));

        var result = WaveSimulator.Simulate(board, Cfg);

        Assert.AreEqual(1, result.Steps.Count);
        var step = result.Steps[0];
        Assert.AreEqual(2, step.Changes.Count);

        var sourceChange = step.Changes.Single(c => c.Side.Equals(new SideId(A, 0)));
        Assert.AreEqual(new TileStack(P1, 4), sourceChange.Before);
        Assert.AreEqual(new TileStack(P1, 1), sourceChange.After);

        var targetChange = step.Changes.Single(c => c.Side.Equals(new SideId(B, 0)));
        Assert.AreEqual(TileStack.Empty, targetChange.Before);
        Assert.AreEqual(new TileStack(P1, 3), targetChange.After);
    }

    [Test]
    public void DeterministicOrder_AProcessedBeforeB()
    {
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
        Assert.AreEqual(new SideId(A, 0), firstChange.Side);
    }
}
