using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Maps;
using WhoWhoo3D.Networking;

namespace WhoWhoo3D.Tests.PlayMode;

public class LanMatchTests
{
    private readonly List<LanMatchSession> _sessions = new();

    private LanMatchSession MakePlayer()
    {
        var obj = new GameObject("LAN test player");
        var game = obj.AddComponent<GameController>();
        game.InitializeWithBoard(MapLoader.Load("sample"));
        var session = obj.AddComponent<LanMatchSession>();
        session.Bind(game);
        _sessions.Add(session);
        return session;
    }

    private IEnumerator Until(Func<bool> ready)
    {
        float deadline = Time.realtimeSinceStartup + 6;
        while (!ready())
        {
            Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline),
                string.Join(", ", _sessions.Select(s => $"{s.State} {s.Status} revision {s.Revision}")));
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator HostAndGuestsFinishReconnectAndRematch()
    {
        var host = MakePlayer();
        host.Host("demo4x4", 3, 0);
        var guest = MakePlayer();
        string address = host.Address.Split(' ')[0];
        guest.Join(address, host.Port);
        yield return Until(() => guest.LocalSeat == 2);
        Assert.That(host.State, Is.EqualTo(LanState.Lobby));
        MakePlayer().Join(address, host.Port);
        yield return Until(() => _sessions.All(s => s.State == LanState.Playing));
        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, _sessions.Select(s => s.LocalSeat));

        var games = _sessions.Select(s => s.GetComponent<GameController>()).ToArray();
        var board = games[0];
        var rng = new System.Random(42);
        int moves = 0;
        while (!board.IsGameOver)
        {
            int active = board.ActivePlayer.Id.Value - 1;
            var slot = BoardActions.LegalMoves(board.Board, board.ActivePlayer.Id).OrderBy(_ => rng.Next()).First();
            Assert.That(games[(active + 1) % games.Length].TryPlayerAction(slot), Is.False);
            Assert.That(games[active].TryPlayerAction(slot), Is.True);
            Assert.That(games[active].TryPlayerAction(slot), Is.False);
            moves++;
            Assert.That(moves, Is.LessThan(500));
            yield return Until(() => _sessions.All(s => s.Revision == moves));
            string expected = JsonUtility.ToJson(MatchSnapshot.Capture(board));
            foreach (var game in games.Skip(1))
                Assert.That(JsonUtility.ToJson(MatchSnapshot.Capture(game)), Is.EqualTo(expected));

            if (moves == 4)
            {
                // Close the underlying TCP socket without sending an intentional room leave.
                var field = typeof(LanMatchSession).GetField("_server", BindingFlags.Instance | BindingFlags.NonPublic);
                ((LanPeer)field.GetValue(guest)).Dispose();
                yield return Until(() => guest.CanReconnect && host.State == LanState.Lobby);
                Assert.That(board.AcceptsBoardInput, Is.False);
                guest.Reconnect();
                yield return Until(() => _sessions.All(s => s.State == LanState.Playing));
                Assert.That(guest.LocalSeat, Is.EqualTo(2));
                foreach (var game in games)
                    Assert.That(JsonUtility.ToJson(MatchSnapshot.Capture(game)), Is.EqualTo(expected));
            }
        }
        host.Rematch();
        yield return Until(() => _sessions.All(s => s.Revision == 0));
        Assert.That(games.All(g => !g.IsGameOver && g.TurnNumber == 1 && g.MapName == "demo4x4"), Is.True);
        guest.Leave();
        yield return Until(() => _sessions.All(s => s.State == LanState.Closed));
        Assert.That(games.All(g => g.InSetup && g.Connection == null), Is.True);
    }

    [UnityTest]
    public IEnumerator HostRejectsWrongSeatAndReplayedGuestTurn()
    {
        var host = MakePlayer();
        host.Host("demo4x4", 2, 0);
        var game = host.GetComponent<GameController>();
        using var client = new TcpClient();
        client.Connect(IPAddress.Loopback, host.Port);
        using var wire = new LanPeer(client);
        void Send(LanMessage message) { wire.Send(message); wire.Flush(); }
        IEnumerator Receive(string kind) => Until(() => wire.Read()?.kind == kind);

        Send(new LanMessage { kind = "hello", version = 1 });
        yield return Receive("welcome");
        Send(new LanMessage { kind = "ready" });
        yield return Receive("snapshot");

        var slot = BoardActions.LegalMoves(game.Board, new PlayerId(1))[0];
        Send(LanMessage.Move("claim", 1, 0, new ClaimSlotCommand(new PlayerId(1), 1, slot)));
        yield return Receive("rejected");
        Assert.That(host.Revision, Is.Zero);
        Assert.That(game.TryPlayerAction(slot), Is.True);
        yield return Receive("accepted");
        yield return Until(() => host.Revision == 1);

        slot = BoardActions.LegalMoves(game.Board, new PlayerId(2))[0];
        var guestClaim = LanMessage.Move("claim", 1, 0, new ClaimSlotCommand(new PlayerId(2), game.TurnNumber, slot));
        Send(guestClaim);
        Send(guestClaim);
        yield return Receive("accepted");
        yield return Receive("rejected");
        Assert.That(host.Revision, Is.EqualTo(2));
    }

    [UnityTearDown]
    public IEnumerator CleanUp()
    {
        foreach (var session in _sessions)
        {
            session.Leave();
            UnityEngine.Object.Destroy(session.gameObject);
        }
        _sessions.Clear();
        yield return null;
    }
}
