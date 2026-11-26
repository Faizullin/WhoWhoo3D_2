using System;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Networking;

[Serializable]
internal sealed class LanMessage
{
    public string kind;
    public int version;
    public int seat;
    public int players;
    public string map;
    public string digest;
    public string token;
    public string text;
    public int round;
    public int sequence;
    public int turn;
    public string cell;
    public int side;
    public int edge;
    public MatchSnapshot snapshot;

    public ClaimSlotCommand Claim => new(new PlayerId(seat), turn, new SlotId(new SideId(new CellId(cell), side), edge));

    public static LanMessage Move(string kind, int round, int sequence, ClaimSlotCommand claim) => new()
    {
        kind = kind, round = round, sequence = sequence, seat = claim.Actor.Value, turn = claim.TurnNumber,
        cell = claim.Slot.Side.Cell.Value, side = claim.Slot.Side.LocalIndex, edge = claim.Slot.Edge
    };
}
