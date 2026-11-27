using System.Linq;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Gameplay.Ai;

internal static class ComputerAbilities
{
    public static bool TryPlay(GameController game)
    {
        var actor = game.ActivePlayer.Id;
        var rules = game.Abilities;
        var inventory = game.ActivePlayer.Inventory;
        if (rules.CanRefill(MatchAction.Beacon))
            game.TrySubmitClaim(new ClaimSlotCommand(actor, game.TurnNumber, default, MatchAction.Supply, Refill: MatchAction.Beacon));

        var next = rules.NextOpponent;
        ClaimSlotCommand? freeze = null, beacon = null;
        foreach (var cell in game.Board.Cells.Values)
        foreach (var face in cell.ExposedSides)
        {
            if (face.SlotCount == 0) continue;
            if (inventory.freeze && freeze == null && !next.IsNone && face.OwnedCount(next) > 0 &&
                face.OwnedCount(next) == face.SlotCount - 1 &&
                face.Slots.Any(s => game.Board.GetSide(s.Partner.Side).OwnedCount(actor) > 0) && rules.CanFreeze(cell.Id))
                freeze = new ClaimSlotCommand(actor, game.TurnNumber, face.Slots.First().Id, MatchAction.Freeze);
            if (inventory.beacon && beacon == null && face.OwnedCount(actor) + 2 == face.SlotCount)
            {
                var white = face.Slots.Where(s => rules.CanClaim(s.Id)).ToList();
                if (white.Count == 2)
                    beacon = new ClaimSlotCommand(actor, game.TurnNumber, white[0].Id, MatchAction.Beacon, white[1].Id.Edge);
            }
            if (!inventory.relay) continue;
            foreach (var source in face.Slots)
            {
                if (!rules.TryRelay(source.Id, out var to, out _)) continue;
                var landing = game.Board.GetSide(to.Side);
                if (landing.OwnedCount(actor) + 2 == landing.SlotCount &&
                    game.TrySubmitClaim(new ClaimSlotCommand(actor, game.TurnNumber, source.Id, MatchAction.Relay))) return true;
            }
        }
        var choice = freeze ?? beacon;
        return choice.HasValue && game.TrySubmitClaim(choice.Value);
    }
}
