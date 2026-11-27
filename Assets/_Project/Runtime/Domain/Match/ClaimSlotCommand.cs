using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Domain.Match;

public enum MatchAction { Claim, Relay, Freeze, Beacon, Supply }

/// <summary>A seat's decision at a turn boundary. Beacon chooses two edges on the same face.</summary>
public readonly record struct ClaimSlotCommand(PlayerId Actor, int TurnNumber, SlotId Slot,
    MatchAction Action = MatchAction.Claim, int SecondEdge = -1, MatchAction Refill = MatchAction.Claim)
{
    public ClaimSlotCommand(PlayerId actor, int turnNumber, SlotId slot, bool relay)
        : this(actor, turnNumber, slot, relay ? MatchAction.Relay : MatchAction.Claim) { }
}
