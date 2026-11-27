using System;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;

namespace WhoWhoo3D.Gameplay.Abilities;

/// <summary>One seat's charges and placed effects at a settled turn. Also the wire representation.</summary>
[Serializable]
public sealed class AbilityInventory
{
    public const int SupplyThreshold = 4;
    public bool relay = true, freeze = true, beacon;
    public int ordinaryClaims;
    public bool supplyUsed;
    public string frozenCell;
    public int thawAfterTurn;
    public string beaconCell;
    public int beaconSide, firstEdge, secondEdge;

    public bool SupplyReady => ordinaryClaims == SupplyThreshold && !supplyUsed;
    public bool HasBeacon => !string.IsNullOrEmpty(beaconCell);
    public SlotId BeaconFirst => new(new SideId(new CellId(beaconCell), beaconSide), firstEdge);
    public SlotId BeaconSecond => new(BeaconFirst.Side, secondEdge);
    public bool Has(MatchAction card) => card switch
    {
        MatchAction.Relay => relay, MatchAction.Freeze => freeze, MatchAction.Beacon => beacon, _ => false
    };

    internal void SetCharge(MatchAction card, bool charged)
    {
        switch (card)
        {
            case MatchAction.Relay: relay = charged; break;
            case MatchAction.Freeze: freeze = charged; break;
            case MatchAction.Beacon: beacon = charged; break;
            default: throw new ArgumentOutOfRangeException(nameof(card));
        }
    }

    internal AbilityInventory Copy() => (AbilityInventory)MemberwiseClone();

    internal bool ValidOn(BoardModel board, int turn)
    {
        if (ordinaryClaims < 0 || ordinaryClaims > SupplyThreshold || (supplyUsed && ordinaryClaims != SupplyThreshold))
            return false;
        if (!string.IsNullOrEmpty(frozenCell) && (!board.Cells.ContainsKey(new CellId(frozenCell)) ||
            thawAfterTurn < turn || thawAfterTurn > turn + 1)) return false;
        if (!HasBeacon) return true;
        return board.Cells.TryGetValue(new CellId(beaconCell), out var cell) &&
            beaconSide >= 0 && beaconSide < cell.Sides.Count && firstEdge != secondEdge &&
            cell.GetSide(beaconSide).GetSlot(firstEdge) != null && cell.GetSide(beaconSide).GetSlot(secondEdge) != null;
    }
}
