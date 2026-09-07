using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Player;
using WhoWhoo3D.Tests.EditMode.Support;

namespace WhoWhoo3D.Tests.EditMode.Turn;

public class PlayerRosterTests
{
    private static readonly PlayerId P1 = new(1);
    private static readonly PlayerId P2 = new(2);
    private static readonly PlayerId P3 = new(3);

    [Test]
    public void PlayerState_ScoreCountsOwnedTiles()
    {
        var board = TestBoards.SingleCube();
        var side = board.GetSide(new SideId(new CellId("cube"), 0));
        var slots = side.Slots.ToList();

        board.SetOwner(slots[0].Id, P1);
        board.SetOwner(slots[1].Id, P1);

        Assert.AreEqual(2, new PlayerState(P1).ComputeScore(board));
        Assert.AreEqual(0, new PlayerState(P2).ComputeScore(board));
    }

    [Test]
    public void PlayerRoster_StartsAtFirstPlayer()
    {
        var roster = new PlayerRoster(new List<PlayerState> { new(P1), new(P2), new(P3) });

        Assert.AreEqual(P1, roster.ActivePlayer.Id);
        Assert.AreEqual(3, roster.AliveCount);
    }

    [Test]
    public void PlayerRoster_AdvanceToNextAlive_CyclesThroughPlayers()
    {
        var roster = new PlayerRoster(new List<PlayerState> { new(P1), new(P2), new(P3) });

        roster.AdvanceToNextAlive();
        Assert.AreEqual(P2, roster.ActivePlayer.Id);
        roster.AdvanceToNextAlive();
        Assert.AreEqual(P3, roster.ActivePlayer.Id);
        roster.AdvanceToNextAlive();
        Assert.AreEqual(P1, roster.ActivePlayer.Id);
    }

    [Test]
    public void PlayerRoster_SkipsEliminatedPlayers()
    {
        var p1 = new PlayerState(P1);
        var p2 = new PlayerState(P2) { IsAlive = false };
        var p3 = new PlayerState(P3);
        var roster = new PlayerRoster(new List<PlayerState> { p1, p2, p3 });

        roster.AdvanceToNextAlive();
        Assert.AreEqual(P3, roster.ActivePlayer.Id);
        roster.AdvanceToNextAlive();
        Assert.AreEqual(P1, roster.ActivePlayer.Id);
    }

    [Test]
    public void UpdateAliveStatus_KeepsAPlayerWhoHasNotClaimedAnythingYetAlive()
    {
        // The opening board is all white. Nobody has tiles, and nobody is out.
        var board = TestBoards.SingleCube();
        var p1 = new PlayerState(P1);
        var p2 = new PlayerState(P2);
        var roster = new PlayerRoster(new List<PlayerState> { p1, p2 });

        roster.UpdateAliveStatus(board);

        Assert.IsTrue(p1.IsAlive);
        Assert.IsTrue(p2.IsAlive);
        Assert.AreEqual(2, roster.AliveCount);
    }

    [Test]
    public void UpdateAliveStatus_EliminatesAPlayerWhoHeldTilesAndLostThemAll()
    {
        var board = TestBoards.SingleCube();
        var slot = board.GetSide(new SideId(new CellId("cube"), 0)).Slots.First().Id;

        var p1 = new PlayerState(P1);
        var p2 = new PlayerState(P2);
        var roster = new PlayerRoster(new List<PlayerState> { p1, p2 });

        board.SetOwner(slot, P2);
        roster.UpdateAliveStatus(board);
        Assert.IsTrue(p2.IsAlive, "Precondition: P2 is on the board.");
        Assert.IsTrue(p2.HasHeldTiles);

        board.SetOwner(slot, PlayerId.None);
        roster.UpdateAliveStatus(board);

        Assert.IsFalse(p2.IsAlive, "Having held tiles and lost them all is elimination.");
        Assert.IsTrue(p1.IsAlive, "P1 has never claimed anything, so P1 is still in.");
    }

}

