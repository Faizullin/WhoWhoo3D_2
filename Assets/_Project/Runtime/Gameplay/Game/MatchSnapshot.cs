using System;
using System.Collections.Generic;
using System.Linq;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Game;

/// <summary>A settled turn boundary. Slots use ordinal cell ID, side index and edge order.</summary>
[Serializable]
public sealed class MatchSnapshot
{
    public int turn;
    public int active;
    public int skipped;
    public bool finished;
    public int winner;
    public int[] owners;
    public bool[] alive;
    public bool[] held;

    private static IEnumerable<Slot> Slots(BoardModel board) =>
        board.Cells.OrderBy(p => p.Key.Value, StringComparer.Ordinal).SelectMany(p => p.Value.Sides)
            .SelectMany(side => side.Slots);

    public static MatchSnapshot Capture(GameController game) => new()
    {
        turn = game.TurnNumber, active = game.ActivePlayer.Id.Value,
        skipped = game.ConsecutiveSkippedTurns, finished = game.IsGameOver, winner = game.Winner.Value,
        owners = Slots(game.Board).Select(slot => slot.Owner.Value).ToArray(),
        alive = game.Roster.Players.Select(p => p.IsAlive).ToArray(),
        held = game.Roster.Players.Select(p => p.HasHeldTiles).ToArray()
    };

    internal void RestoreBoard(BoardModel board, int count)
    {
        var slots = Slots(board).ToArray();
        if (owners == null || slots.Length != owners.Length || alive?.Length != count || held?.Length != count ||
            turn < 1 || active < 1 || active > count || skipped < 0 || skipped > count ||
            winner < 0 || winner > count || (!finished && (winner != 0 || !alive[active - 1])) ||
            owners.Any(id => id < 0 || id > count))
            throw new ArgumentException("Invalid match snapshot.");
        for (int i = 0; i < slots.Length; i++) board.SetOwner(slots[i].Id, new PlayerId(owners[i]));
    }
}
