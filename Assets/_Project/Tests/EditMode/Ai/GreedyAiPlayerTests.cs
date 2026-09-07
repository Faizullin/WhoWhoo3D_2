using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Gameplay.Ai;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Turn;
using WhoWhoo3D.Tests.EditMode.Support;

namespace WhoWhoo3D.Tests.EditMode.Ai;

public class GreedyAiPlayerTests
{
    private static readonly PlayerId P1 = TestBoards.P1;
    private static readonly PlayerId P2 = TestBoards.P2;
    private static readonly CellId Cube = new("cube");

    private GreedyAiPlayer _ai;

    [SetUp]
    public void SetUp() => _ai = new GreedyAiPlayer(WaveConfig.Default);

    private static BoardModel TwoPlayerBoard()
    {
        var board = TestBoards.SingleCube("cube");
        board.SetOwner(board.GetSide(new SideId(Cube, 0)).Slots.First().Id, P1);
        board.SetOwner(board.GetSide(new SideId(Cube, 1)).Slots.First().Id, P2);
        return board;
    }

    [Test]
    public void TryChooseMove_ReturnsFalseWhenNothingIsClaimable()
    {
        var board = TestBoards.SingleCube("cube");

        // Every face holds a P1 tile, so P2 is locked out everywhere.
        foreach (var side in board.GetCell(Cube).Sides)
            board.SetOwner(side.Slots.First().Id, P1);

        Assert.IsFalse(_ai.TryChooseMove(board, P2, out _));
    }

    [Test]
    public void TryChooseMove_OnlyPicksLegalSlots()
    {
        var board = TwoPlayerBoard();

        Assert.IsTrue(_ai.TryChooseMove(board, P1, out var chosen));
        Assert.IsTrue(BoardActions.CanClaim(board, chosen, P1));
    }

    [Test]
    public void TryChooseMove_PrefersCompletingAFaceOverAnIdleTile()
    {
        var board = TestBoards.SingleCube("cube");
        var face = new SideId(Cube, 0);
        var slots = board.GetSide(face).Slots.ToList();

        // One slot short of triggering an expansion that captures four partners.
        for (int i = 0; i < slots.Count - 1; i++)
            board.SetOwner(slots[i].Id, P1);

        Assert.IsTrue(_ai.TryChooseMove(board, P1, out var chosen));
        Assert.AreEqual(slots[^1].Id, chosen,
            "Completing the face gains four tiles; any other click gains one.");
    }

    [Test]
    public void EvaluateAll_ScoresEveryLegalMoveInSlotOrder()
    {
        var board = TwoPlayerBoard();

        var evaluations = _ai.EvaluateAll(board, P1);
        var expected = BoardActions.LegalMoves(board, P1);

        Assert.AreEqual(expected.Count, evaluations.Count);
        CollectionAssert.AreEqual(expected, evaluations.Select(e => e.Slot).ToList());
    }

    [Test]
    public void TryChooseMove_DoesNotMutateTheLiveBoard()
    {
        var board = TwoPlayerBoard();
        int p1Before = BoardActions.ScoreFor(board, P1);
        int p2Before = BoardActions.ScoreFor(board, P2);

        _ai.TryChooseMove(board, P1, out _);

        Assert.AreEqual(p1Before, BoardActions.ScoreFor(board, P1));
        Assert.AreEqual(p2Before, BoardActions.ScoreFor(board, P2));
        Assert.IsTrue(board.Validate().IsValid);
    }

    [Test]
    public void TryChooseMove_IsDeterministic()
    {
        var board = TwoPlayerBoard();

        _ai.TryChooseMove(board, P1, out var first);
        _ai.TryChooseMove(board, P1, out var second);

        Assert.AreEqual(first, second);
    }

    [Test]
    public void AiTurnExecutesOnTickAndPassesPlay()
    {
        var holder = new GameObject("AiGame");
        try
        {
            var game = holder.AddComponent<GameController>();
            // humanPlayer 0 matches nobody, so every player is AI-controlled.
            game.ConfigureAi(enabled: true, humanPlayer: 0, thinkDelay: 0f);
            game.InitializeWithBoard(TwoPlayerBoard());

            Assert.IsTrue(((PlayerTurnPhase)game.CurrentPhase).IsAiTurn);
            Assert.AreEqual(P1, game.ActivePlayer.Id);

            game.Tick(0f);

            Assert.AreEqual(P2, game.ActivePlayer.Id, "AI should have played and passed the turn.");
            Assert.Greater(BoardActions.ScoreFor(game.Board, P1), 1);
        }
        finally { Object.DestroyImmediate(holder); }
    }

    [Test]
    public void AiVersusAiGameProgressesWithoutStalling()
    {
        var holder = new GameObject("AiGame");
        try
        {
            var game = holder.AddComponent<GameController>();
            game.ConfigureAi(enabled: true, humanPlayer: 0, thinkDelay: 0f);
            game.InitializeWithBoard(TwoPlayerBoard());

            const int maxTurns = 400;
            int turns = 0;
            while (!game.IsGameOver && turns < maxTurns)
            {
                game.Tick(0f);
                turns++;
            }

            Assert.IsTrue(game.Board.Validate().IsValid, game.Board.Validate().ToString());
            Assert.Greater(turns, 0);
            if (game.IsGameOver)
                Assert.AreEqual(1, game.Roster.AliveCount);
        }
        finally { Object.DestroyImmediate(holder); }
    }
}

