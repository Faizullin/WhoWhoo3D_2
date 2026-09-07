using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.History;
using WhoWhoo3D.Tests.EditMode.Support;

namespace WhoWhoo3D.Tests.EditMode.History;

public class GameHistoryTests
{
    private static readonly PlayerId P1 = TestBoards.P1;
    private static readonly PlayerId P2 = TestBoards.P2;
    private static readonly CellId Cube = new("cube");

    private static HistoryEntry Entry(SlotId slot, PlayerId before, PlayerId after) =>
        new(HistoryKind.Claim, 1, after, "test",
            new[] { new SlotStateChange(slot, before, after, SlotChangeCause.Claimed, Origin: null) });

    private static SlotId AnySlot(BoardModel board, int face = 0) =>
        board.GetSide(new SideId(Cube, face)).Slots.First().Id;

    [Test]
    public void NewHistoryStartsEmptyAndAtThePresent()
    {
        var history = new GameHistory();

        Assert.AreEqual(-1, history.Cursor);
        Assert.IsTrue(history.IsAtPresent);
        Assert.IsFalse(history.CanStepBack);
        Assert.IsFalse(history.CanStepForward);
    }

    [Test]
    public void RecordAdvancesTheCursorAndStaysAtThePresent()
    {
        var board = TestBoards.SingleCube("cube");
        var history = new GameHistory();

        history.Record(Entry(AnySlot(board), PlayerId.None, P1));

        Assert.AreEqual(0, history.Cursor);
        Assert.IsTrue(history.IsAtPresent);
        Assert.IsTrue(history.CanStepBack);
    }

    [Test]
    public void SteppingBackAndForwardReturnsTheSameEntry()
    {
        var board = TestBoards.SingleCube("cube");
        var history = new GameHistory();
        var slot = AnySlot(board);
        history.Record(Entry(slot, PlayerId.None, P1));

        var back = history.StepBack();
        Assert.IsNotNull(back);
        Assert.AreEqual(-1, history.Cursor);
        Assert.IsFalse(history.IsAtPresent);

        var forward = history.StepForward();
        Assert.AreSame(back, forward);
        Assert.IsTrue(history.IsAtPresent);
    }

    [Test]
    public void SteppingPastEitherEndReturnsNull()
    {
        var history = new GameHistory();

        Assert.IsNull(history.StepBack());
        Assert.IsNull(history.StepForward());
    }

    [Test]
    public void RecordingWhileScrubbedDropsTheAbandonedFuture()
    {
        var board = TestBoards.SingleCube("cube");
        var history = new GameHistory();
        var a = AnySlot(board, 0);
        var b = AnySlot(board, 1);
        var c = AnySlot(board, 2);

        history.Record(Entry(a, PlayerId.None, P1));
        history.Record(Entry(b, PlayerId.None, P2));
        history.StepBack();

        history.Record(Entry(c, PlayerId.None, P1));

        Assert.AreEqual(2, history.Entries.Count, "The abandoned branch is dropped.");
        Assert.IsTrue(history.IsAtPresent);
    }

    [Test]
    public void ReversingEveryEntryRestoresTheStartingBoardExactly()
    {
        // The property the whole feature rests on: Before/After makes history exactly
        // reversible, with no inverse-operation logic to get wrong.
        var board = TestBoards.SingleCube("cube");
        var holder = new GameObject("g");
        try
        {
            var game = holder.AddComponent<GameController>();
            game.ConfigureAi(enabled: false, humanPlayer: 1);
            game.ConfigurePlayers(2);
            game.InitializeWithBoard(board);

            var opening = board.Cells.Values
                .SelectMany(cell => cell.Sides)
                .SelectMany(side => side.Slots)
                .ToDictionary(s => s.Id, s => s.Owner);

            // Play until something has actually happened.
            for (int i = 0; i < 12 && !game.IsGameOver; i++)
            {
                var move = WhoWhoo3D.Gameplay.Board.BoardActions
                    .LegalMoves(board, game.ActivePlayer.Id).FirstOrDefault();
                if (move.Equals(default(SlotId))) break;
                game.OnSlotClicked(move);
            }

            Assert.Greater(game.History.Entries.Count, 0, "Precondition: something happened.");

            foreach (var entry in game.History.Entries.Reverse())
                foreach (var change in entry.Changes)
                    board.SetOwner(change.Slot, change.Before);

            foreach (var pair in opening)
                Assert.AreEqual(pair.Value, board.GetSlot(pair.Key).Owner,
                    $"Slot {pair.Key} did not return to its opening owner.");
            Assert.IsTrue(board.Validate().IsValid);
        }
        finally { Object.DestroyImmediate(holder); }
    }

    [Test]
    public void ClaimsAndWaveStepsAreRecordedSeparately()
    {
        var board = TestBoards.SingleCube("cube");
        var face = new SideId(Cube, 0);
        var slots = board.GetSide(face).Slots.ToList();
        for (int i = 0; i < slots.Count - 1; i++)
            board.SetOwner(slots[i].Id, P1);

        var holder = new GameObject("g");
        try
        {
            var game = holder.AddComponent<GameController>();
            game.ConfigureAi(enabled: false, humanPlayer: 1);
            game.ConfigurePlayers(2);
            game.InitializeWithBoard(board);

            game.OnSlotClicked(slots[^1].Id);

            var kinds = game.History.Entries.Select(e => e.Kind).ToList();
            Assert.AreEqual(HistoryKind.Claim, kinds[0], "The claim is its own entry.");
            Assert.Contains(HistoryKind.WaveStep, kinds,
                "Each chain step is recorded separately so a wave can be stepped through.");
        }
        finally { Object.DestroyImmediate(holder); }
    }

    [Test]
    public void RevertToCursorRewindsTheLiveBoardAndDropsTheFuture()
    {
        var board = TestBoards.SingleCube("cube");
        var holder = new GameObject("g");
        try
        {
            var game = holder.AddComponent<GameController>();
            game.ConfigureAi(enabled: false, humanPlayer: 1);
            game.ConfigurePlayers(2);
            game.InitializeWithBoard(board);

            var first = WhoWhoo3D.Gameplay.Board.BoardActions
                .LegalMoves(board, game.ActivePlayer.Id).First();
            game.OnSlotClicked(first);
            int afterFirst = game.History.Entries.Count;

            var second = WhoWhoo3D.Gameplay.Board.BoardActions
                .LegalMoves(board, game.ActivePlayer.Id).First();
            game.OnSlotClicked(second);

            game.StepHistoryBack();
            game.RevertToCursor();

            Assert.AreEqual(afterFirst, game.History.Entries.Count, "The future is dropped.");
            Assert.IsTrue(board.GetSlot(second).IsNeutral, "The reverted claim is undone.");
            Assert.IsFalse(game.IsReviewingHistory);
            Assert.IsTrue(board.Validate().IsValid);
        }
        finally { Object.DestroyImmediate(holder); }
    }

    [Test]
    public void ReviewingBlocksPlayUntilYouReturnToThePresent()
    {
        var board = TestBoards.SingleCube("cube");
        var holder = new GameObject("g");
        try
        {
            var game = holder.AddComponent<GameController>();
            game.ConfigureAi(enabled: false, humanPlayer: 1);
            game.ConfigurePlayers(2);
            game.InitializeWithBoard(board);

            var move = WhoWhoo3D.Gameplay.Board.BoardActions
                .LegalMoves(board, game.ActivePlayer.Id).First();
            game.OnSlotClicked(move);

            game.StepHistoryBack();
            Assert.IsTrue(game.IsReviewingHistory);

            var next = WhoWhoo3D.Gameplay.Board.BoardActions
                .LegalMoves(board, game.ActivePlayer.Id).First();
            int before = game.History.Entries.Count;
            game.OnSlotClicked(next);
            Assert.AreEqual(before, game.History.Entries.Count,
                "Clicks are ignored while reviewing — the arrows are a viewer, not an undo.");

            game.ReturnToPresent();
            Assert.IsFalse(game.IsReviewingHistory);
            Assert.IsTrue(game.History.IsAtPresent);
        }
        finally { Object.DestroyImmediate(holder); }
    }
}

