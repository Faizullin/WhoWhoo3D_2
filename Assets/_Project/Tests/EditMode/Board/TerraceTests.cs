using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Maps;
using WhoWhoo3D.Input;

namespace WhoWhoo3D.Tests.EditMode.Board;

public class TerraceTests
{
    [Test]
    public void CornerStartsHaveEqualAccessToTheSummit()
    {
        var board = MapLoader.Load("terrace6x6");
        Assert.That(board.Validate().IsValid, Is.True);
        Assert.That(board.Cells.Count, Is.EqualTo(52));
        // Walk the playable face graph outward from the four summit faces.
        var queue = new Queue<SideId>(board.Cells.Values.Where(c => c.Position.Y == 2).Select(c => c.GetSide(0).Id));
        var distance = queue.ToDictionary(id => id, _ => 0);
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            foreach (var slot in board.GetSide(id).Slots)
            {
                var next = slot.Partner.Side;
                if (distance.ContainsKey(next)) continue;
                distance.Add(next, distance[id] + 1);
                queue.Enqueue(next);
            }
        }
        var faces = board.Cells.Values.SelectMany(c => c.ExposedSides).Where(s => s.SlotCount > 0).ToList();
        Assert.That(distance.Count, Is.EqualTo(faces.Count));
        var seeds = faces.SelectMany(s => s.Slots).Where(s => !s.IsNeutral).ToList();
        Assert.That(seeds.Select(s => s.Owner.Value), Is.EquivalentTo(new[] {1, 2, 3, 4}));
        Assert.That(seeds.Select(s => distance[s.Id.Side]).Distinct().Count(), Is.EqualTo(1));
        Assert.That(seeds.Select(s => BoardActions.LegalMoves(board, s.Owner).Count).Distinct().Count(), Is.EqualTo(1));
    }

    [Test]
    public void CameraFitsTheTerraceAfterResizeAndBoundsPanAndZoom()
    {
        var obj = new GameObject("Terrace camera");
        try
        {
            var board = MapLoader.Load("terrace6x6");
            var camera = obj.AddComponent<Camera>();
            var rig = new BoardCamera(camera);
            foreach (float aspect in new[] { 16f / 9, 9f / 16, 21f / 9 })
            {
                camera.aspect = aspect;
                rig.Frame(board);
                foreach (var cell in board.Cells.Values)
                {
                    var p = cell.Position;
                    for (int x = -1; x <= 1; x += 2)
                        for (int y = -1; y <= 1; y += 2)
                            for (int z = -1; z <= 1; z += 2)
                            {
                                var corner = new Vector3(p.X + .5f * x, p.Y + .5f * y, p.Z + .5f * z);
                                var screen = camera.WorldToViewportPoint(corner);
                                Assert.That(screen.x, Is.InRange(0f, 1f));
                                Assert.That(screen.y, Is.InRange(0f, 1f));
                                Assert.That(screen.z, Is.GreaterThan(camera.nearClipPlane));
                            }
                }
            }
            var home = obj.transform.position;
            foreach (float sign in new[] { 1f, -1f })
            {
                rig.Zoom(sign * 100);
                var zoomLimit = obj.transform.position;
                rig.Zoom(sign * 100);
                Assert.That(obj.transform.position, Is.EqualTo(zoomLimit));
                rig.Pan(Vector2.one * sign * 100000);
                var panLimit = obj.transform.position;
                rig.Pan(Vector2.one * sign * 100000);
                Assert.That(obj.transform.position, Is.EqualTo(panLimit));
            }
            rig.Reset();
            Assert.That(obj.transform.position, Is.EqualTo(home));
        }
        finally { Object.DestroyImmediate(obj); }
    }

    [Test]
    public void RelayPreviewCancelConfirmAndSpentChargeAreConsistent()
    {
        var obj = new GameObject("Terrace inventory");
        try
        {
            var game = obj.AddComponent<GameController>();
            game.StartMatchWithoutPresentation(new MatchSetup("terrace6x6", new SeatControl[2]));
            var actor = game.ActivePlayer.Id;
            var source = game.Board.Cells.Values.SelectMany(c => c.ExposedSides).SelectMany(s => s.Slots)
                .Single(s => s.Owner == actor);
            Assert.That(BoardActions.TryRelay(game.Board, source.Id, actor, out var landing, out var bonus), Is.True);
            game.ChooseRelay(true);
            game.OnSlotClicked(source.Id);
            Assert.That(game.RelaySource, Is.EqualTo(source.Id));
            game.ChooseRelay(false);
            Assert.That(game.ConfirmRelay(), Is.False);
            Assert.That(game.History.Entries, Is.Empty);
            Assert.That(game.ActivePlayer.RelaySpent, Is.False);
            var rejected = new ClaimSlotCommand(actor, game.TurnNumber, landing, true);
            Assert.That(game.TrySubmitClaim(rejected), Is.False);
            game.ChooseRelay(true);
            game.OnSlotClicked(source.Id);
            Assert.That(game.ConfirmRelay(), Is.True);
            var changes = game.History.Entries[0].Changes;
            Assert.That(changes.Select(c => c.Slot), Is.EqualTo(new[] { source.Id, landing, bonus }));
            Assert.That(game.Board.GetSlot(source.Id).IsNeutral, Is.True);
            Assert.That(game.Roster.Players[0].RelaySpent, Is.True);
            Assert.That(game.TryPlayerAction(BoardActions.LegalMoves(game.Board, game.ActivePlayer.Id)[0]), Is.True);
            Assert.That(game.TrySubmitClaim(new ClaimSlotCommand(actor, game.TurnNumber, landing, true)), Is.False);
            while (game.StepHistoryBack()) { }
            game.RevertToCursor();
            Assert.That(game.Roster.Players[0].RelaySpent, Is.False);
            Assert.That(game.Board.GetSlot(source.Id).Owner, Is.EqualTo(actor));
        }
        finally { Object.DestroyImmediate(obj); }
    }
}
