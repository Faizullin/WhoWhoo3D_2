using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Domain.Match;

/// <summary>A claim addressed to one turn of the current match.</summary>
public readonly record struct ClaimSlotCommand(PlayerId Actor, int TurnNumber, SlotId Slot, bool Relay = false);
