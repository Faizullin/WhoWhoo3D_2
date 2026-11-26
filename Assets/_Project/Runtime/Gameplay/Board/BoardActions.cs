using System.Collections.Generic;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Board;

/// <summary>
/// Shared selection and scoring rules for a board.
/// </summary>
public static class BoardActions
{
    /// <summary>
    /// A face is open to a player when nobody else holds a tile on it. One enemy tile
    /// locks the whole face — you have to take it by expansion instead.
    /// </summary>
    public static bool CanSelectSide(Side side, PlayerId player)
    {
        if (side.IsInterior || side.SlotCount == 0) return false;

        foreach (var slot in side.Slots)
            if (!slot.IsNeutral && !slot.Owner.Equals(player))
                return false;

        return true;
    }

    /// <summary>A slot is claimable when it is white and its face is not held by someone else.</summary>
    public static bool CanClaim(BoardModel board, SlotId slotId, PlayerId player)
    {
        if (player.IsNone) return false;

        if (!board.Cells.TryGetValue(slotId.Side.Cell, out var cell)) return false;
        int sideIndex = slotId.Side.LocalIndex;
        if (sideIndex < 0 || sideIndex >= cell.Sides.Count) return false;

        var side = cell.GetSide(sideIndex);
        var slot = side.GetSlot(slotId.Edge);
        return slot != null && slot.IsNeutral && CanSelectSide(side, player);
    }

    /// <summary>
    /// Claimable slots in deterministic (cell, side, edge) order.
    /// </summary>
    public static List<SlotId> LegalMoves(BoardModel board, PlayerId player)
    {
        var moves = new List<SlotId>();
        if (player.IsNone) return moves;

        foreach (var cell in board.Cells.Values)
        {
            foreach (var side in cell.Sides)
            {
                if (!CanSelectSide(side, player)) continue;

                foreach (var slot in side.Slots)
                    if (slot.IsNeutral)
                        moves.Add(slot.Id);
            }
        }

        moves.Sort(CompareSlots);
        return moves;
    }

    public static bool HasLegalMove(BoardModel board, PlayerId player)
    {
        if (player.IsNone) return false;

        foreach (var cell in board.Cells.Values)
        {
            foreach (var side in cell.Sides)
            {
                if (!CanSelectSide(side, player)) continue;

                foreach (var slot in side.Slots)
                    if (slot.IsNeutral)
                        return true;
            }
        }

        return false;
    }

    /// <summary>Tiles held by the player. This is the score.</summary>
    public static int ScoreFor(BoardModel board, PlayerId player)
    {
        if (player.IsNone) return 0;

        int score = 0;
        foreach (var cell in board.Cells.Values)
            score += cell.OwnedCount(player);
        return score;
    }

    /// <summary>Tiles held by everyone except the player. White tiles count for nobody.</summary>
    public static int OpponentScore(BoardModel board, PlayerId player)
    {
        int score = 0;
        foreach (var cell in board.Cells.Values)
            foreach (var side in cell.Sides)
                foreach (var slot in side.Slots)
                    if (!slot.IsNeutral && !slot.Owner.Equals(player))
                        score++;
        return score;
    }

    private static int CompareSlots(SlotId a, SlotId b)
    {
        int c = string.CompareOrdinal(a.Side.Cell.Value, b.Side.Cell.Value);
        if (c != 0) return c;
        c = a.Side.LocalIndex.CompareTo(b.Side.LocalIndex);
        return c != 0 ? c : a.Edge.CompareTo(b.Edge);
    }
}

