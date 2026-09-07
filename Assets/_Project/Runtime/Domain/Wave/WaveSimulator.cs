using System;
using System.Collections.Generic;
using System.Linq;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Domain.Wave;

/// <summary>
/// Resolves expansions until the board settles.
///
/// A face expands when every slot on it is claimed by one player. Expanding pushes that
/// player onto the partner slot of each of its slots — the tile across the shared edge —
/// and then releases the whole source face back to white. A face therefore trades its own
/// tiles for a foothold on each neighbouring face, which is what makes chains possible.
///
/// Landing on a slot another player holds takes that player's entire face, not just the
/// one slot (see <see cref="WaveConfig.ConquestTakesFace"/>).
///
/// Mutates the board in place. Callers needing a non-destructive run clone first.
/// </summary>
public static class WaveSimulator
{
    public static WaveResult Simulate(BoardModel board, WaveConfig cfg)
    {
        cfg.Validate();
        var steps = new List<WaveStep>();
        bool decided = false;

        for (int i = 0; i < cfg.MaxChainSteps; i++)
        {
            var full = FindFullSides(board);
            if (full.Count == 0)
                break;

            var step = new WaveStep();

            foreach (var sideId in full)
                Expand(board, sideId, cfg, step);

            if (step.Changes.Count == 0)
                break;

            steps.Add(step);

            // The moment the last opponent is wiped out the game is over. Continuing to
            // chain would churn a board nobody will play on -- and on a board one player
            // has saturated, every face is full, so the chain would run to MaxChainSteps
            // releasing and refilling itself for no reason.
            if (IsDecided(board))
            {
                decided = true;
                break;
            }
        }

        bool quiescent = FindFullSides(board).Count == 0;
        return new WaveResult(steps, quiescent, decided);
    }

    /// <summary>At most one player still holds tiles, so nothing is left to contest.</summary>
    private static bool IsDecided(BoardModel board)
    {
        var holder = PlayerId.None;

        foreach (var cell in board.Cells.Values)
        {
            foreach (var side in cell.Sides)
            {
                foreach (var slot in side.Slots)
                {
                    if (slot.IsNeutral) continue;

                    if (holder.IsNone) holder = slot.Owner;
                    else if (!holder.Equals(slot.Owner)) return false;
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Faces ready to expand, ordered by (cellId, sideIndex) so a wave replays identically
    /// on every machine — the property the deterministic multiplayer plan depends on.
    /// </summary>
    private static List<SideId> FindFullSides(BoardModel board)
    {
        var result = new List<SideId>();

        foreach (var (_, cell) in board.Cells.OrderBy(kv => kv.Key.Value, StringComparer.Ordinal))
        {
            foreach (var side in cell.Sides)
                if (side.IsFull(out _))
                    result.Add(side.Id);
        }

        return result;
    }

    private static void Expand(BoardModel board, SideId sourceId, WaveConfig cfg, WaveStep step)
    {
        var source = board.GetSide(sourceId);

        // An earlier expansion this step may have taken a slot here and broken the run.
        if (!source.IsFull(out var owner))
            return;

        // Resolve arrivals before releasing the source, and remember which source slot each
        // tile left from so the animation can draw it travelling from the right place.

        foreach (var slot in source.Slots.ToList())
            Claim(board, slot.Partner, owner, cfg, step, from: slot.Id);

        foreach (var slot in source.Slots)
        {
            if (slot.IsNeutral) continue;

            // The source spends its tiles: they go back to white and anyone may take them.
            Record(step, slot.Id, slot.Owner, PlayerId.None, SlotChangeCause.Released);
            slot.Owner = PlayerId.None;
        }
    }

    private static void Claim(
        BoardModel board, SlotId arrivalId, PlayerId owner, WaveConfig cfg, WaveStep step, SlotId from)
    {
        var arrival = board.GetSlot(arrivalId)
            ?? throw new InvalidOperationException(
                $"Slot {arrivalId} is the partner of an expanding slot but does not exist. " +
                "Tiles must exist in pairs; the board is inconsistent.");

        // The arriving tile needs a home of its own, so a FREE slot is always preferred.
        //
        // Landing on an enemy tile instead would merely repaint something conquest is about
        // to take anyway (R4), so the expansion would add nothing to the attacker's count --
        // the face would change colour without growing. Landing on our own tile is worse
        // still: it evaporates.
        //
        // Preference order: the tile's own slot if it is free (so it crosses its own edge,
        // which is also what the animation draws), then any free slot on that face, and
        // only then the original slot.
        SlotId targetId;
        if (arrival.IsNeutral)
            targetId = arrivalId;
        else
            targetId = FindFreeSlotOn(board, arrivalId.Side) ?? arrivalId;

        var target = board.GetSlot(targetId);

        // Nowhere useful to land: the whole face is already ours. The tile is spent.
        if (target.Owner.Equals(owner))
            return;

        var before = target.Owner;
        Record(step, targetId, before, owner, SlotChangeCause.Expanded, from);
        target.Owner = owner;

        if (!cfg.ConquestTakesFace) return;

        // Taking a foothold on a face takes the whole face: every tile the defender held
        // there changes hands too, not just the one that was targeted. This is what makes
        // an expansion into enemy territory feel decisive, and it keeps the "one player
        // per face" invariant, so a face can never end up contested and unclickable.
        // White slots are left alone -- they stay open for anyone to claim.
        foreach (var sibling in board.GetSide(targetId.Side).Slots)
        {
            if (sibling.Id.Equals(targetId) || sibling.IsNeutral) continue;
            if (sibling.Owner.Equals(owner)) continue;

            // Absorbed, not expanded: it changes hands where it stands. Recording the
            // distinction is what stops the animator drawing a tile flying in from this
            // slot's own partner, which is somewhere else entirely.
            Record(step, sibling.Id, sibling.Owner, owner, SlotChangeCause.Absorbed);
            sibling.Owner = owner;
        }
    }

    /// <summary>Lowest-edge white slot on a face, or null when there is none.</summary>
    private static SlotId? FindFreeSlotOn(BoardModel board, SideId sideId)
    {
        foreach (var slot in board.GetSide(sideId).Slots)
            if (slot.IsNeutral)
                return slot.Id;

        return null;
    }

    private static void Record(
        WaveStep step, SlotId slot, PlayerId before, PlayerId after,
        SlotChangeCause cause, SlotId? origin = null)
    {
        if (!before.Equals(after))
            step.Changes.Add(new SlotStateChange(slot, before, after, cause, origin));
    }
}
