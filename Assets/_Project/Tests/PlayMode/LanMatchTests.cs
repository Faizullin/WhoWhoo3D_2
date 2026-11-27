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
            if (moves == 1)
            {
                var source = board.Board.Cells.Values.SelectMany(c => c.ExposedSides).SelectMany(s => s.Slots)
                    .First(s => BoardActions.TryRelay(board.Board, s.Id, board.ActivePlayer.Id, out _, out _));
                games[active].ChooseRelay(true);
                games[active].OnSlotClicked(source.Id);
                Assert.That(games[active].ConfirmRelay(), Is.True);
            }
            else Assert.That(games[active].TryPlayerAction(slot), Is.True);
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
                Assert.That(games.All(g => g.Roster.Players[1].RelaySpent), Is.True);
                foreach (var game in games)
                    Assert.That(JsonUtility.ToJson(MatchSnapshot.Capture(game)), Is.EqualTo(expected));
            }
        }
        host.Rematch();
        yield return Until(() => _sessions.All(s => s.Revision == 0));
        Assert.That(games.All(g => !g.IsGameOver && g.TurnNumber == 1 && g.MapName == "demo4x4"), Is.True);
        Assert.That(games.All(g => g.Roster.Players.All(p => !p.RelaySpent)), Is.True);
        guest.Leave();
        yield return Until(() => _sessions.All(s => s.State == LanState.Closed));
        Assert.That(games.All(g => g.InSetup && g.Connection == null), Is.True);
    }

    [UnityTest]
    public IEnumerator BrowserTracksSeatsJoinsAndExpiresRooms()
    {
        var host = MakePlayer();
        string name = "Room " + Guid.NewGuid().ToString("N").Substring(0, 8);
        var browser = MakePlayer();
        browser.BrowseRooms();
        LanRoomListing Listing() => browser.NearbyRooms.SingleOrDefault(room => room.Name == name);

        host.Host("demo4x4", 3, 0, name);
        yield return Until(() => Listing()?.OpenSeats == 2);
        Assert.That(Listing().Capacity, Is.EqualTo(3));
        Assert.That(Listing().Port, Is.EqualTo(host.Port));
        Assert.That(Listing().Map, Is.EqualTo("demo4x4"));
        var guest = MakePlayer();
        guest.Join(Listing());
        yield return Until(() => Listing()?.OpenSeats == 1);
        var lastGuest = MakePlayer();
        lastGuest.Join(Listing());
        yield return Until(() => host.State == LanState.Playing && lastGuest.LocalSeat == 3);
        yield return Until(() => Listing() == null);

        host.Leave();
        host.Host("sample", 2, 0, name);
        yield return Until(() => Listing()?.Map == "sample");
        var previousRoom = Listing();
        host.Leave();
        yield return Until(() => Listing() == null);

        host.Host("sample", 2, previousRoom.Port, name);
        var staleGuest = MakePlayer();
        staleGuest.Join(previousRoom);
        yield return Until(() => staleGuest.State == LanState.Disconnected);
        Assert.That(staleGuest.Status, Does.Contain("room has closed"));
        Assert.That(host.State, Is.EqualTo(LanState.Lobby));
        yield return Until(() => Listing() != null);
        browser.Join(Listing());
        yield return Until(() => browser.State == LanState.Playing);
        Assert.That(browser.IsBrowsing, Is.False);
        Assert.That(browser.NearbyRooms, Is.Empty);
    }

    [UnityTest]
    public IEnumerator BrowserListsOnlyCompatibleAnnouncements()
    {
        var browser = MakePlayer();
        browser.BrowseRooms();
        yield return Until(() => browser.DiscoveryAvailable);
        using var sender = new UdpClient();
        var endpoint = new IPEndPoint(IPAddress.Loopback, RoomAnnouncement.DiscoveryPort);
        var message = new RoomAnnouncement { game = RoomAnnouncement.Signature, version = LanMatchSession.ProtocolVersion,
            name = "Protocol test", map = "sample", port = 47771, capacity = 2, open = 1 };
        void Announce()
        {
            message.id = Guid.NewGuid().ToString("N");
            var bytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(message));
            sender.Send(bytes, bytes.Length, endpoint);
        }
        message.version++;
        Announce();
        string incompatible = message.id;
        message.version--;
        message.open = 0;
        Announce();
        string full = message.id;
        message.open = 1;
        Announce();
        yield return Until(() => browser.NearbyRooms.Any(r => r.Id == message.id));
        Assert.That(browser.NearbyRooms.Any(r => r.Id == incompatible || r.Id == full), Is.False);
        browser.StopBrowsing();
        Assert.That(browser.NearbyRooms, Is.Empty);
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

        Send(new LanMessage { kind = "hello", version = LanMatchSession.ProtocolVersion });
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
