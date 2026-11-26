using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Turn;
using WhoWhoo3D.Tests.EditMode.Support;

namespace WhoWhoo3D.Tests.EditMode.Turn;

public class TurnLoopTests
{
    private static readonly PlayerId P1 = TestBoards.P1;
    private static readonly PlayerId P2 = TestBoards.P2;
    private static readonly CellId Cube = TestBoards.At(0, 0, 0);

    private GameObject _holder;
    private GameController _game;

    [SetUp]
    public void SetUp()
    {
        _holder = new GameObject("TestGameController");
        _game = _holder.AddComponent<GameController>();

        // Human-only so these tests exercise the click path; AI is covered separately.
        _game.ConfigureAi(enabled: false, humanPlayer: 1);
    }

    [TearDown]
    public void TearDown()
    {
        if (_holder != null) Object.DestroyImmediate(_holder);
    }

    /// <summary>One cube; P1 holds a tile on face 0, P2 holds one on face 1.</summary>
    private static BoardModel TwoPlayerBoard()
    {
        var board = TestBoards.SingleCube();
        board.SetOwner(board.GetSide(new SideId(Cube, 0)).Slots.First().Id, P1);
        board.SetOwner(board.GetSide(new SideId(Cube, 1)).Slots.First().Id, P2);
        return board;
    }

    private static SlotId FirstWhiteSlotOn(BoardModel board, int face) =>
        board.GetSide(new SideId(Cube, face)).Slots.First(s => s.IsNeutral).Id;

    [Test]
    public void InitializeWithBoard_SetsUpActivePlayerAndPhases()
    {
        _game.InitializeWithBoard(TwoPlayerBoard());

        Assert.AreEqual(2, _game.Roster.Players.Count);
        Assert.AreEqual(P1, _game.ActivePlayer.Id);
        Assert.IsInstanceOf<PlayerTurnPhase>(_game.CurrentPhase);
        Assert.IsFalse(_game.IsGameOver);
    }

    [Test]
    public void TryPlayerAction_ClaimsAWhiteTileOnTheActivePlayersFace()
    {
        var board = TwoPlayerBoard();
        _game.InitializeWithBoard(board);
        var target = FirstWhiteSlotOn(board, 0);

        Assert.IsTrue(_game.TryPlayerAction(target));
        Assert.AreEqual(P1, board.GetSlot(target).Owner);
    }

    [Test]
    public void TryPlayerAction_RefusesAFaceHeldByAnotherPlayer()
    {
        var board = TwoPlayerBoard();
        _game.InitializeWithBoard(board);

        // Face 1 already holds a P2 tile, so P1 is locked out of the whole face.
        var target = FirstWhiteSlotOn(board, 1);

        Assert.IsFalse(_game.TryPlayerAction(target));
        Assert.IsTrue(board.GetSlot(target).IsNeutral);
    }

    [Test]
    public void TryPlayerAction_RefusesAnAlreadyClaimedTile()
    {
        var board = TwoPlayerBoard();
        _game.InitializeWithBoard(board);
        var taken = board.GetSide(new SideId(Cube, 0)).Slots.First(s => !s.IsNeutral).Id;

        Assert.IsFalse(_game.TryPlayerAction(taken));
    }

    [TestCase(2, 1)]
    [TestCase(1, 0)]
    [TestCase(1, 2)]
    public void ClaimForAnotherPlayerOrTurnLeavesMatchUntouched(int actor, int turn)
    {
        var board = TwoPlayerBoard();
        _game.InitializeWithBoard(board);
        var target = FirstWhiteSlotOn(board, 0);
        var command = new ClaimSlotCommand(new PlayerId(actor), turn, target);

        Assert.IsFalse(_game.TrySubmitClaim(command));
        Assert.IsTrue(board.GetSlot(target).IsNeutral);
        Assert.AreEqual(P1, _game.ActivePlayer.Id);
        Assert.AreEqual(1, _game.TurnNumber);
        Assert.IsEmpty(_game.History.Entries);
    }

    [Test]
    public void ReplayingAnAcceptedClaimCannotConsumeAnotherTurn()
    {
        _game.InitializeWithBoard(TwoPlayerBoard());
        var command = new ClaimSlotCommand(P1, 1, FirstWhiteSlotOn(_game.Board, 0));
        Assert.IsTrue(_game.TrySubmitClaim(command));
        Assert.IsFalse(_game.TrySubmitClaim(command));
        Assert.AreEqual(2, _game.TurnNumber);
        Assert.AreEqual(P2, _game.ActivePlayer.Id);
        Assert.AreEqual(1, _game.History.Entries.Count);

        _game.OnSlotClicked(FirstWhiteSlotOn(_game.Board, 1));
        var legalNow = FirstWhiteSlotOn(_game.Board, 0);
        Assert.AreEqual(P1, _game.ActivePlayer.Id);
        Assert.IsFalse(_game.TrySubmitClaim(new ClaimSlotCommand(P1, 1, legalNow)));
        Assert.IsTrue(_game.Board.GetSlot(legalNow).IsNeutral);
        Assert.AreEqual(3, _game.TurnNumber);
        Assert.AreEqual(2, _game.History.Entries.Count);
    }

    [TestCase("missing-cell", 0, 0)]
    [TestCase(null, 20, 0)]
    [TestCase(null, 0, -1)]
    public void UnknownClaimTargetIsRejected(string cellName, int side, int edge)
    {
        _game.InitializeWithBoard(TwoPlayerBoard());
        var cell = cellName == null ? Cube : new CellId(cellName);
        var slot = new SlotId(new SideId(cell, side), edge);
        Assert.IsFalse(_game.TrySubmitClaim(new ClaimSlotCommand(P1, 1, slot)));
        Assert.AreEqual(1, _game.TurnNumber);
        Assert.IsEmpty(_game.History.Entries);
    }

    [Test]
    public void ExplicitCommandsRespectPauseHistoryAndSetup()
    {
        _game.InitializeWithBoard(TwoPlayerBoard());
        var first = new ClaimSlotCommand(P1, 1, FirstWhiteSlotOn(_game.Board, 0));
        _game.SetPaused(true);
        Assert.IsFalse(_game.TrySubmitClaim(first));
        _game.SetPaused(false);
        Assert.IsTrue(_game.TrySubmitClaim(first));

        var second = new ClaimSlotCommand(P2, 2, FirstWhiteSlotOn(_game.Board, 1));
        Assert.IsTrue(_game.StepHistoryBack());
        Assert.IsFalse(_game.TrySubmitClaim(second));
        _game.ReturnToPresent();
        _game.OpenSetup();
        Assert.IsFalse(_game.TrySubmitClaim(second));
        Assert.AreEqual(2, _game.TurnNumber);
        Assert.AreEqual(1, _game.History.Entries.Count);
        Assert.IsTrue(_game.Board.GetSlot(second.Slot).IsNeutral);
    }

    [Test]
    public void OnSlotClicked_ResolvesTurnWithoutWaitingForUpdate()
    {
        var board = TwoPlayerBoard();
        _game.InitializeWithBoard(board);
        var target = FirstWhiteSlotOn(board, 0);

        // Edit Mode never calls Update(), so a transition that only happened there
        // would never run.
        _game.OnSlotClicked(target);

        Assert.AreEqual(P1, board.GetSlot(target).Owner);
        Assert.AreEqual(P2, _game.ActivePlayer.Id, "Turn should have passed to P2.");
    }

    [Test]
    public void OnSlotClicked_IgnoredWhenTheMoveIsIllegal()
    {
        var board = TwoPlayerBoard();
        _game.InitializeWithBoard(board);

        _game.OnSlotClicked(FirstWhiteSlotOn(board, 1));

        Assert.AreEqual(P1, _game.ActivePlayer.Id, "An invalid click must not consume the turn.");
    }

    [Test]
    public void FillingAFaceTriggersExpansionDuringTheWavePhase()
    {
        var board = TestBoards.SingleCube();
        var face = new SideId(Cube, 0);
        var slots = board.GetSide(face).Slots.ToList();

        // P1 one tile short of a full face; P2 alive elsewhere.
        for (int i = 0; i < slots.Count - 1; i++) board.SetOwner(slots[i].Id, P1);
        board.SetOwner(board.GetSide(new SideId(Cube, 1)).Slots.First().Id, P2);

        var partners = slots.Select(s => s.Partner).ToList();
        _game.InitializeWithBoard(board);

        _game.OnSlotClicked(slots[^1].Id);

        Assert.IsTrue(board.GetSide(face).Slots.All(s => s.IsNeutral),
            "The filled face expands and releases its tiles.");
        foreach (var partner in partners)
            Assert.AreEqual(P1, board.GetSlot(partner).Owner);
    }

    [Test]
    public void AnAllWhiteBoardStartsAGameInsteadOfEndingIt()
    {
        // The shipped map seeds nothing. Players come from the configured count, and
        // nobody is eliminated for not having claimed a tile yet.
        _game.ConfigurePlayers(2);
        _game.InitializeWithBoard(TestBoards.SingleCube());

        Assert.IsFalse(_game.IsGameOver, "An empty opening board is a start, not a finish.");
        Assert.AreEqual(2, _game.Roster.Players.Count);
        Assert.AreEqual(2, _game.Roster.AliveCount);
        Assert.IsInstanceOf<PlayerTurnPhase>(_game.CurrentPhase);
    }

    [Test]
    public void PlayerCountComesFromConfigurationNotFromTheMap()
    {
        _game.ConfigurePlayers(4);
        _game.InitializeWithBoard(TestBoards.SingleCube());

        Assert.AreEqual(4, _game.Roster.Players.Count);
    }

    [Test]
    public void AnyPlayerMayClaimOnAnAllWhiteFace()
    {
        _game.ConfigurePlayers(2);
        var board = TestBoards.SingleCube();
        _game.InitializeWithBoard(board);

        var slot = board.GetSide(new SideId(Cube, 0)).Slots.First().Id;
        Assert.IsTrue(_game.TryPlayerAction(slot));
        Assert.AreEqual(P1, board.GetSlot(slot).Owner);
    }

    [Test]
    public void AStalledRoundEndsTheGameOnScoreRatherThanSpinning()
    {
        // If every player is skipped for want of a legal move, the phase machine would
        // otherwise cycle until its transition cap tripped and halted the game with an
        // error. A full round of skips ends it on tiles held instead.
        var board = TestBoards.SingleCube();
        var faceA = board.GetSide(new SideId(Cube, 0)).Slots.ToList();
        board.SetOwner(faceA[0].Id, P1);
        board.SetOwner(faceA[1].Id, P1);
        board.SetOwner(board.GetSide(new SideId(Cube, 1)).Slots.First().Id, P2);

        _game.ConfigurePlayers(2);
        _game.InitializeWithBoard(board);

        for (int i = 0; i < _game.Roster.Players.Count; i++)
            _game.NotifyTurnSkipped();
        Assert.IsTrue(_game.EveryoneIsStuck);

        var check = new VictoryCheckPhase(_game);
        check.Enter();

        Assert.IsTrue(check.GameOver, "A stalled round must end the game.");
        Assert.AreEqual(P1, check.Winner, "P1 holds more tiles, so P1 wins the stalemate.");
    }

    [Test]
    public void LeadingPlayerIsNoneOnATie()
    {
        var board = TestBoards.SingleCube();
        board.SetOwner(board.GetSide(new SideId(Cube, 0)).Slots.First().Id, P1);
        board.SetOwner(board.GetSide(new SideId(Cube, 1)).Slots.First().Id, P2);

        _game.ConfigurePlayers(2);
        _game.InitializeWithBoard(board);

        Assert.AreEqual(PlayerId.None, _game.LeadingPlayer());
    }

    [Test]
    public void PlayingAMoveClearsTheStallCounter()
    {
        var board = TwoPlayerBoard();
        _game.InitializeWithBoard(board);

        _game.NotifyTurnSkipped();
        Assert.AreEqual(1, _game.ConsecutiveSkippedTurns);

        _game.TryPlayerAction(FirstWhiteSlotOn(board, 0));

        Assert.AreEqual(0, _game.ConsecutiveSkippedTurns);
    }

    [Test]
    public void DisplayScoreFallsBackToTheModelWhenNothingIsAnimating()
    {
        var board = TwoPlayerBoard();
        _game.InitializeWithBoard(board);

        foreach (var player in _game.Roster.Players)
            Assert.AreEqual(player.ComputeScore(board), _game.DisplayScoreFor(player.Id),
                "With no animator running, what is drawn is the model.");
    }

    [Test]
    public void DisplayIsOutOnlyAfterAPlayerHasHeldTiles()
    {
        var board = TestBoards.SingleCube();
        _game.ConfigurePlayers(2);
        _game.InitializeWithBoard(board);

        var p2 = _game.Roster.Players.First(p => p.Id.Equals(P2));
        Assert.IsFalse(_game.DisplayIsOut(p2),
            "A player who has never claimed anything is not 'out' on an empty board.");

        p2.HasHeldTiles = true;
        Assert.IsTrue(_game.DisplayIsOut(p2),
            "Once they have held tiles, zero means out.");
    }

    [Test]
    public void InitializeWithBoard_ResetsGameOverState()
    {
        _game.InitializeWithBoard(TwoPlayerBoard());
        Assert.IsFalse(_game.IsGameOver);

        _game.InitializeWithBoard(TwoPlayerBoard());

        Assert.IsFalse(_game.IsGameOver);
        Assert.AreEqual(PlayerId.None, _game.Winner);
        Assert.AreEqual(P1, _game.ActivePlayer.Id);
    }
}

