using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.History;

namespace WhoWhoo3D.Tests.EditMode.Turn;

public class MatchReplayTests
{
    [TestCase("demo4x4")]
    [TestCase("terrace6x6")]
    public void ReplayingAcceptedClaimsOnAnIndependentBoardMatchesTheHostThroughVictory(string map)
    {
        var hostObject = new GameObject("Host");
        var clientObject = new GameObject("Client");
        try
        {
            var host = StartMatch(hostObject, map);
            var client = StartMatch(clientObject, map);
            var random = new System.Random(42);
            bool sawExpansion = false;
            while (!host.IsGameOver)
            {
                Assert.Less(host.TurnNumber, 500, "The demo match must finish.");
                var legal = BoardActions.LegalMoves(host.Board, host.ActivePlayer.Id);
                var slot = legal.OrderBy(_ => random.Next()).First();
                var claim = new ClaimSlotCommand(host.ActivePlayer.Id, host.TurnNumber, slot);
                int start = host.History.Entries.Count;

                Assert.IsTrue(host.TrySubmitClaim(claim));
                Assert.AreEqual(start, client.History.Entries.Count, "The client advances on delivery.");
                Assert.IsTrue(client.TrySubmitClaim(claim));
                Assert.IsFalse(client.TrySubmitClaim(claim), "Delivering a claim twice must have no effect.");

                Assert.AreEqual(host.History.Entries.Count, client.History.Entries.Count);
                for (int i = start; i < host.History.Entries.Count; i++)
                {
                    var expected = host.History.Entries[i];
                    var actual = client.History.Entries[i];
                    Assert.AreEqual(expected.Kind, actual.Kind);
                    Assert.AreEqual(expected.TurnNumber, actual.TurnNumber);
                    Assert.AreEqual(expected.Actor, actual.Actor);
                    CollectionAssert.AreEqual(expected.Changes, actual.Changes);
                    sawExpansion |= actual.Kind == HistoryKind.WaveStep;
                }

                foreach (var cell in host.Board.Cells.Values)
                    foreach (var side in cell.ExposedSides)
                        foreach (var tile in side.Slots)
                            Assert.AreEqual(tile.Owner, client.Board.GetSlot(tile.Id).Owner, tile.Id.ToString());

                Assert.AreEqual(host.TurnNumber, client.TurnNumber);
                Assert.AreEqual(host.ActivePlayer.Id, client.ActivePlayer.Id);
                Assert.AreEqual(host.IsGameOver, client.IsGameOver);
                Assert.AreEqual(host.Winner, client.Winner);
                CollectionAssert.AreEqual(host.Roster.Players.Select(p => (p.Id, p.IsAlive, p.HasHeldTiles)),
                    client.Roster.Players.Select(p => (p.Id, p.IsAlive, p.HasHeldTiles)));
            }
            Assert.IsTrue(sawExpansion);
            Assert.IsFalse(client.Winner.IsNone);
        }
        finally
        {
            Object.DestroyImmediate(hostObject);
            Object.DestroyImmediate(clientObject);
        }
    }

    private static GameController StartMatch(GameObject holder, string map)
    {
        var game = holder.AddComponent<GameController>();
        game.StartMatchWithoutPresentation(new MatchSetup(map, new SeatControl[3]));
        return game;
    }
}
