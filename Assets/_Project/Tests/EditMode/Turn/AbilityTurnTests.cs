using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.History;
using WhoWhoo3D.Tests.EditMode.Support;

namespace WhoWhoo3D.Tests.EditMode.Turn;

public class AbilityTurnTests
{
    private GameObject _holder;
    private GameController _game;

    [SetUp]
    public void SetUp()
    {
        _holder = new GameObject("Ability match");
        _game = _holder.AddComponent<GameController>();
    }

    [TearDown]
    public void TearDown() => Object.DestroyImmediate(_holder);

    private bool Play(MatchAction action, SlotId slot, int second = -1) =>
        _game.TrySubmitClaim(new ClaimSlotCommand(_game.ActivePlayer.Id, _game.TurnNumber, slot, action, second));

    [Test]
    public void SinglePlayableCubeCannotBeFrozen()
    {
        var board = TestBoards.SingleCube();
        _game.InitializeWithBoard(board);
        var target = board.Cells.Values.First().Sides.First().Slots.First().Id;
        Assert.That(Play(MatchAction.Freeze, target), Is.False);
        Assert.That(_game.ActivePlayer.Inventory.freeze, Is.True);
    }

    [Test]
    public void IceBlocksDirectClaimsAndRelayLandingsButAnIncomingWaveStillConquers()
    {
        var board = TestBoards.TwoCubes();
        var first = board.GetSide(new SideId(TestBoards.At(0, 0, 0), 0));
        var seam = first.Slots.Single(s => s.Partner.Side.Cell != first.Id.Cell);
        var victim = seam.Partner.Side;
        var blue = TestBoards.P2;
        foreach (var s in first.Slots.Where(s => s != seam)) board.SetOwner(s.Id, blue);
        var relaySource = board.GetSide(victim).Slots.Select(s => s.Partner)
            .First(id => id.Side != first.Id);
        board.SetOwner(relaySource, blue);
        var last = board.GetSide(victim).Slots.First().Id;
        _game.InitializeWithBoard(board);
        Assert.That(Play(MatchAction.Freeze, last), Is.True);
        Assert.That(BoardActions.TryRelay(board, relaySource, blue, out _, out _), Is.True);
        Assert.That(Play(MatchAction.Relay, relaySource), Is.False);
        Assert.That(_game.TryPlayerAction(last), Is.False);
        Assert.That(_game.TryPlayerAction(seam.Id), Is.True);
        Assert.That(board.GetSide(victim).OwnedCount(blue), Is.GreaterThan(0));
        Assert.That(_game.Abilities.Frozen(victim.Cell), Is.False);
    }

    [TestCase(2, false)]
    [TestCase(3, true)]
    [TestCase(4, false)]
    public void FreezeSurvivesReconnectAndExpiresAfterOneOpposingSeat(int players, bool restore)
    {
        var setup = new MatchSetup("terrace6x6", new SeatControl[players]);
        _game.StartMatchWithoutPresentation(setup);
        var target = BoardActions.LegalMoves(_game.Board, _game.ActivePlayer.Id)
            .First(slot => _game.Abilities.CanTarget(MatchAction.Freeze, slot));
        _game.SelectAbility(MatchAction.Freeze);
        _game.OnSlotClicked(target);
        _game.SelectAbility(MatchAction.Claim);
        Assert.That(_game.TurnNumber, Is.EqualTo(1));
        Assert.That(_game.ActivePlayer.Inventory.freeze, Is.True);
        Assert.That(Play(MatchAction.Freeze, target), Is.True);
        if (restore)
        {
            var json = JsonUtility.ToJson(MatchSnapshot.Capture(_game));
            _game.RestoreMatch(setup, JsonUtility.FromJson<MatchSnapshot>(json));
        }
        Assert.That(_game.Abilities.Frozen(target.Side.Cell), Is.True);
        Assert.That(_game.Roster.Players[0].Inventory.freeze, Is.False);
        Assert.That(_game.ActivePlayer.Id.Value, Is.EqualTo(2));
        var reply = BoardActions.LegalMoves(_game.Board, _game.ActivePlayer.Id).First(_game.Abilities.CanClaim);
        Assert.That(_game.TryPlayerAction(reply), Is.True);
        Assert.That(_game.Abilities.Frozen(target.Side.Cell), Is.False);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void BeaconSurvivesReconnectAndResolvesOnceBeforeItsOwnersAction(bool interrupt)
    {
        var setup = new MatchSetup("terrace6x6", true, new SeatControl[2]);
        _game.StartMatchWithoutPresentation(setup);
        for (int i = 0; i < 8; i++)
        {
            var move = BoardActions.LegalMoves(_game.Board, _game.ActivePlayer.Id)
                .First(s => _game.Board.GetSide(s.Side).SlotCount == 4 && _game.Board.GetSide(s.Side).Slots.All(t => t.IsNeutral));
            Assert.That(_game.TryPlayerAction(move), Is.True);
        }
        Assert.That(_game.TakeSupply(MatchAction.Freeze), Is.False, "A full card cannot be refilled.");
        Assert.That(_game.TakeSupply(MatchAction.Beacon), Is.True);
        Assert.That(_game.TakeSupply(MatchAction.Beacon), Is.False);
        Assert.That(_game.TurnNumber, Is.EqualTo(9));
        var marks = _game.Board.Cells.Values.SelectMany(c => c.ExposedSides)
            .First(f => f.SlotCount == 4 && f.Slots.All(s => s.IsNeutral)).Slots.Take(2).ToArray();
        Assert.That(Play(MatchAction.Beacon, marks[0].Id, marks[0].Id.Edge), Is.False);
        Assert.That(Play(MatchAction.Beacon, marks[0].Id, marks[1].Id.Edge), Is.True);
        _game.RestoreMatch(setup, JsonUtility.FromJson<MatchSnapshot>(JsonUtility.ToJson(MatchSnapshot.Capture(_game))));
        Assert.That(_game.Roster.Players[0].Inventory.HasBeacon, Is.True);
        var quiet = BoardActions.LegalMoves(_game.Board, _game.ActivePlayer.Id)
            .First(s => s.Side != marks[0].Id.Side && _game.Board.GetSide(s.Side).Slots.All(t => t.IsNeutral));
        Assert.That(_game.TryPlayerAction(interrupt ? marks[0].Id : quiet), Is.True);
        Assert.That(_game.ActivePlayer.Id, Is.EqualTo(TestBoards.P1));
        Assert.That(_game.TurnNumber, Is.EqualTo(11));
        Assert.That(_game.ActivePlayer.Inventory.HasBeacon, Is.False);
        Assert.That(_game.Board.GetSlot(marks[1].Id).Owner, Is.EqualTo(interrupt ? PlayerId.None : TestBoards.P1));
        Assert.That(_game.History.Entries.Count(e => e.Kind == HistoryKind.BeaconResult), Is.EqualTo(1));
        Assert.That(_game.ActivePlayer.Inventory.supplyUsed, Is.True);
        Assert.That(_game.AcceptsBoardInput, Is.True);
        Assert.That(_game.History.Entries.Last().Changes.Count, Is.EqualTo(interrupt ? 0 : 2));
        while (_game.StepHistoryBack()) { }
        _game.RevertToCursor();
        Assert.That(_game.ActivePlayer.Id, Is.EqualTo(TestBoards.P2));
        Assert.That(_game.Roster.Players[0].Inventory.HasBeacon, Is.True);
        _game.RestartMatch();
        Assert.That(_game.ActivePlayer.Inventory.ordinaryClaims, Is.Zero);
        Assert.That(_game.ActivePlayer.Inventory.supplyUsed, Is.False);
    }
}
