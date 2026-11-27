using System;
using System.Linq;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.History;

namespace WhoWhoo3D.Gameplay.Abilities;

/// <summary>Rules shared by target previews, local players, computer seats and the LAN authority.</summary>
public sealed class MatchAbilities
{
    private readonly GameController _game;
    private BoardModel Board => _game.Board;
    private PlayerId Actor => _game.ActivePlayer.Id;
    private AbilityInventory Inventory => _game.ActivePlayer.Inventory;

    public MatchAbilities(GameController game) => _game = game;

    public bool Frozen(CellId cell)
    {
        foreach (var seat in _game.Roster.Players)
            if (seat.Inventory.frozenCell == cell.Value && !cell.IsNone) return true;
        return false;
    }

    public PlayerId NextOpponent
    {
        get
        {
            var seats = _game.Roster.Players;
            for (int i = 0; i < seats.Count - 1; i++)
            {
                var seat = seats[(Actor.Value + i) % seats.Count];
                if (seat.IsAlive) return seat.Id;
            }
            return PlayerId.None;
        }
    }

    public bool CanClaim(SlotId slot) => !Frozen(slot.Side.Cell) && BoardActions.CanClaim(Board, slot, Actor);

    public bool TryRelay(SlotId source, out SlotId to, out SlotId bonus) =>
        BoardActions.TryRelay(Board, source, Actor, out to, out bonus) && !Frozen(to.Side.Cell);

    public bool CanFreeze(CellId cell)
    {
        if (Frozen(cell) || !Board.Cells.TryGetValue(cell, out var target)) return false;
        var opponent = NextOpponent;
        // Freeze must affect a legal opposing claim and leave at least one elsewhere.
        return !opponent.IsNone && target.Sides.Any(face => face.Slots.Any(s => s.IsNeutral) &&
            BoardActions.CanSelectSide(face, opponent)) &&
            BoardActions.HasLegalMove(Board, opponent, cell);
    }

    public bool CanTarget(MatchAction action, SlotId slot)
    {
        if (action == MatchAction.Claim) return CanClaim(slot);
        if (!Inventory.Has(action)) return false;
        switch (action)
        {
            case MatchAction.Relay: return TryRelay(slot, out _, out _);
            case MatchAction.Freeze: return ExistingSlot(slot) && CanFreeze(slot.Side.Cell);
            case MatchAction.Beacon:
                return !Inventory.HasBeacon && CanClaim(slot) && Board.GetSide(slot.Side).Slots.Count(s => s.IsNeutral) >= 2;
            default: return false;
        }
    }

    private bool ExistingSlot(SlotId slot) => Board.Cells.TryGetValue(slot.Side.Cell, out var cell) &&
        slot.Side.LocalIndex >= 0 && slot.Side.LocalIndex < cell.Sides.Count &&
        cell.GetSide(slot.Side.LocalIndex).GetSlot(slot.Edge) != null;

    public bool CanRefill(MatchAction card) => _game.Setup.Supply && Inventory.SupplyReady &&
        (card == MatchAction.Relay || card == MatchAction.Freeze || card == MatchAction.Beacon) && !Inventory.Has(card);

    private static SlotStateChange Gain(SlotId slot, PlayerId actor, SlotId? from = null) =>
        new(slot, PlayerId.None, actor, SlotChangeCause.Claimed, from);

    internal bool TryApply(ClaimSlotCommand command, out HistoryKind kind, out SlotStateChange[] changes)
    {
        var slot = command.Slot;
        kind = HistoryKind.Claim;
        changes = Array.Empty<SlotStateChange>();
        if (command.Action == MatchAction.Supply)
        {
            if (!CanRefill(command.Refill)) return false;
            Inventory.SetCharge(command.Refill, true);
            Inventory.supplyUsed = true;
            kind = HistoryKind.Supply;
            return true;
        }
        if (!CanTarget(command.Action, slot)) return false;
        switch (command.Action)
        {
            case MatchAction.Claim:
                changes = new[] { Gain(slot, Actor) };
                if (_game.Setup.Supply && Inventory.ordinaryClaims < AbilityInventory.SupplyThreshold)
                    Inventory.ordinaryClaims++;
                return true;
            case MatchAction.Relay:
                TryRelay(slot, out var to, out var bonus);
                kind = HistoryKind.Relay;
                changes = new[] { new SlotStateChange(slot, Actor, PlayerId.None, SlotChangeCause.Released, null),
                    Gain(to, Actor, slot), Gain(bonus, Actor) };
                break;
            case MatchAction.Freeze:
                kind = HistoryKind.Freeze;
                Inventory.frozenCell = slot.Side.Cell.Value;
                Inventory.thawAfterTurn = _game.TurnNumber + 1;
                break;
            case MatchAction.Beacon:
                var second = new SlotId(slot.Side, command.SecondEdge);
                if (second == slot || !CanClaim(second)) return false;
                kind = HistoryKind.Beacon;
                Inventory.beaconCell = slot.Side.Cell.Value;
                Inventory.beaconSide = slot.Side.LocalIndex;
                Inventory.firstEdge = slot.Edge;
                Inventory.secondEdge = second.Edge;
                break;
        }
        Inventory.SetCharge(command.Action, false);
        return true;
    }

    internal void FinishTurn()
    {
        foreach (var player in _game.Roster.Players)
        {
            var inv = player.Inventory;
            if (inv.thawAfterTurn == _game.TurnNumber)
            {
                inv.frozenCell = null;
                inv.thawAfterTurn = 0;
            }
            if (!player.IsAlive) inv.beaconCell = null;
        }
    }

    internal bool ResolveBeacon()
    {
        if (!Inventory.HasBeacon) return false;
        var first = Inventory.BeaconFirst;
        var second = Inventory.BeaconSecond;
        Inventory.beaconCell = null;
        bool lands = CanClaim(first) && CanClaim(second);
        var changes = lands ? new[] { Gain(first, Actor), Gain(second, Actor) } : Array.Empty<SlotStateChange>();
        foreach (var change in changes) Board.SetOwner(change.Slot, change.After);
        _game.History.Record(new HistoryEntry(HistoryKind.BeaconResult, _game.TurnNumber, Actor,
            $"{Actor} Beacon {(lands ? "landed" : "fizzled")} on {first.Side}", changes));
        _game.RefreshBoardView();
        return lands;
    }
}
