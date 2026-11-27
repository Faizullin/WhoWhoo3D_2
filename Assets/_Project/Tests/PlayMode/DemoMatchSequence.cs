using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Turn;

namespace WhoWhoo3D.Tests.PlayMode;

// The report player and the Play Mode test drive the same scene and pointer path.
public static class DemoMatchSequence
{
    public static IEnumerator Run(GameController game, BoardView board, Camera camera,
        Mouse mouse, Action<string> checkpoint, bool record)
    {
        Require(game.InSetup, "The demo did not open match setup.");
        checkpoint("setup");
        yield return Hold(record ? 14 : 1);
        if (!record)
        {
            var seats = new[] { SeatControl.Computer, SeatControl.LocalHuman, SeatControl.Computer };
            game.StartMatch(new MatchSetup("demo4x4", seats));
            seats[0] = SeatControl.LocalHuman;
            game.ConfigureAi(false, 1);
            game.RestartMatch();
            Require(game.MapName == "demo4x4" && game.PlayerCount == 3 &&
                game.Roster.Players.Select(p => p.IsAi).SequenceEqual(new[] { true, false, true }),
                "Restart must preserve the selected map and the original controller for every seat.");
            var target = BoardActions.LegalMoves(game.Board, game.ActivePlayer.Id)[0];
            game.OnSlotClicked(target);
            Require(game.History.Entries.Count == 0, "Local input took the computer's turn.");
            game.Tick(1f);
            Require(game.History.Entries.Count > 0 && game.History.Entries[0].Actor == new PlayerId(1),
                "Computer seat did not submit a claim.");
        }
        foreach (int count in record ? Array.Empty<int>() : new[] { 2, 4 })
        {
            game.StartLocalMatch("demo4x4", count);
            Require(game.Roster.Players.Count == count && BoardActions.ScoreFor(game.Board, new PlayerId(count + 1)) == 0,
                "The map contains a seed for an absent player.");
            game.StartLocalMatch("sample", count);
            Require(game.Roster.Players.Count == count && game.Board.Cells.Count == 2, "Map or roster selection failed.");
            game.RestartMatch();
            Require(game.PlayerCount == count && game.MapName == "sample", "Restart lost the selected setup.");
        }

        if (!record)
        {
            game.StartMatch(new MatchSetup("demo4x4", new SeatControl[3]));
            game.OpenSetup();
            yield return null;
            yield return null;
        }
        yield return CompactHudSequence.ClickControl(game, mouse, "Start match");
        yield return null;
        Require(game.Board.Cells.Count == 16 && game.Roster.Players.Count == 3, "Unexpected demo setup.");
        var opening = game.Board.Cells.Values.SelectMany(c => c.ExposedSides)
            .SelectMany(face => face.Slots).ToDictionary(slot => slot.Id, slot => slot.Owner);
        if (!record)
        {
            game.SetPaused(true);
            Require(opening.Keys.All(id => !board.GetSlotView(id).IsClaimHighlighted), "Paused tiles still show move hints.");
            game.SetPaused(false);
        }
        var rng = new System.Random(42);
        var panels = game.GetComponents<IBoardInputPanel>();
        bool sawWave = false;
        int moves = 0;
        game.WaveAnimator.StepDuration = record ? 0.55f : 0.05f;
        checkpoint("opening");
        yield return Hold(record ? 10 : 1);
        float deadline = Time.realtimeSinceStartup + 150;
        while (!game.IsGameOver)
        {
            Require(Time.realtimeSinceStartup < deadline && moves < 500, "The match stalled.");
            if (game.CurrentPhase is PlayerTurnPhase)
            {
                var legal = BoardActions.LegalMoves(game.Board, game.ActivePlayer.Id);
                foreach (var cell in game.Board.Cells.Values)
                    foreach (var face in cell.ExposedSides)
                        foreach (var slot in face.Slots)
                            Require(board.GetSlotView(slot.Id).IsClaimHighlighted == legal.Contains(slot.Id),
                                "Incorrect move hint for " + slot.Id);
                bool accepted = false;
                int visible = 0, unobstructed = 0;
                foreach (var slot in legal.OrderBy(_ => rng.Next()))
                {
                    var view = board.GetSlotView(slot);
                    Vector3 point = camera.WorldToScreenPoint(view.transform.position);
                    if (point.z <= 0 || !camera.pixelRect.Contains(point)) continue;
                    visible++;
                    if (panels.Any(panel => panel.CoversScreenPoint(point))) continue;
                    unobstructed++;
                    if (!Physics.Raycast(camera.ScreenPointToRay(point), out var hit) ||
                        hit.collider.GetComponentInParent<SlotView>() != view) continue;
                    int count = game.History.Entries.Count;
                    var state = new MouseState { position = point };
                    InputSystem.QueueStateEvent(mouse, state);
                    yield return Hold(record ? 3 : 1);
                    InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left));
                    for (int i = 0; i < 8 && game.History.Entries.Count == count; i++) yield return null;
                    InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left, false));
                    yield return null;
                    Require(game.History.Entries.Count > count, "Click missed " + slot);
                    moves++;
                    if (moves == 1)
                    {
                        checkpoint("claim");
                        if (!record && game.AcceptsBoardInput)
                        {
                            Require(game.StepHistoryBack(), "Could not review the first claim.");
                            Require(opening.Keys.All(id => !board.GetSlotView(id).IsClaimHighlighted),
                                "History review still shows live moves.");
                            game.ReturnToPresent();
                        }
                    }
                    accepted = true;
                    break;
                }
                Require(accepted, $"No reachable tile on turn {game.TurnNumber}. Legal {legal.Count}, in view {visible}, outside HUD {unobstructed}, viewport {camera.pixelRect}.");
            }
            if (game.WaveAnimator.IsPlaying && !sawWave)
            {
                sawWave = true;
                yield return Hold(record ? 3 : 1);
                checkpoint("wave");
            }
            yield return null;
        }
        Require(sawWave && !game.Winner.IsNone && game.Board.Validate().IsValid, "Expected a wave and a valid victory.");
        Debug.Log($"Demo match completed in {moves} clicks. Winner P{game.Winner.Value}.");
        checkpoint("victory");
        yield return Hold(record ? 18 : 1);
        yield return CompactHudSequence.ClickControl(game, mouse, "Play again");
        Require(game.TurnNumber == 1 && !game.IsGameOver && game.History.Entries.Count == 0 &&
            game.ActivePlayer.Id == new PlayerId(1) && opening.All(pair => game.Board.GetSlot(pair.Key)?.Owner == pair.Value),
            "Restart did not restore the opening position.");
        checkpoint("restart");
        yield return Hold(record ? 14 : 1);

        game.StartMatch(new MatchSetup("terrace6x6", true, new SeatControl[2]));
        yield return Hold(2);
        var initialView = camera.transform.rotation;
        var initialPosition = camera.transform.position;
        checkpoint("terrace");
        yield return Hold(record ? 14 : 1);
        yield return CompactHudSequence.ClickControl(game, mouse, "Ability info");
        checkpoint("inventory-guide");
        yield return Hold(record ? 18 : 1);
        yield return CompactHudSequence.ClickControl(game, mouse, "Close guide");
        var tread = new SideId(new CellId("cell_2_0_0"), 0);
        var riser = new SideId(new CellId("cell_2_1_1"), 5);
        var upper = new SideId(riser.Cell, 0);
        var route = new[] { new SlotId(riser, 0), new SlotId(riser, 2), new SlotId(riser, 3),
            new SlotId(tread, 0), new SlotId(tread, 1), new SlotId(tread, 2) };
        MatchSnapshot beforeClimb = null;
        for (int i = 0; i < route.Length; i++)
        {
            if (i == route.Length - 1)
            {
                var mark = board.GetCellView(tread.Cell).transform.Find($"One claim away {tread.LocalIndex}");
                Require(mark && mark.GetComponent<Renderer>().enabled, "Missing near-expansion mark on the tread");
                beforeClimb = MatchSnapshot.Capture(game);
                checkpoint("one-away");
                yield return Hold(record ? 10 : 1);
            }
            Require(game.TryPlayerAction(route[i]), "Terrace claim failed at " + route[i]);
            if (i == route.Length - 1) break;
            var replies = BoardActions.LegalMoves(game.Board, game.ActivePlayer.Id);
            var reply = replies.First(id => game.Board.GetCell(id.Side.Cell).Position.Z >= 4 &&
                game.Board.GetSide(id.Side).Slots.All(s => s.Owner.IsNone));
            Require(game.TryPlayerAction(reply), "Guest reply failed.");
        }
        Require(game.WaveAnimator.IsPlaying, "Expected an animated climb.");
        yield return Hold(record ? 4 : 1);
        checkpoint("terrace-wave");
        float settledBy = Time.realtimeSinceStartup + 10f;
        var arrival = board.GetSlotView(new SlotId(upper, 3));
        bool landed = false;
        while (!game.AcceptsBoardInput && Time.realtimeSinceStartup < settledBy)
        {
            if (!landed && LandingVisible(arrival))
            {
                landed = true;
                checkpoint("wave-landing");
            }
            yield return null;
        }
        Require(landed && !LandingVisible(arrival), "Landing frame must appear on the upper tread and clear after playback");
        Require(game.AcceptsBoardInput && game.ActivePlayer.Id.Value == 2, "Terrace wave did not settle.");
        Require(game.Board.GetSlot(new SlotId(upper, 3)).Owner == new PlayerId(1), "Wave missed the upper terrace.");
        if (!record)
        {
            string expected = JsonUtility.ToJson(MatchSnapshot.Capture(game));
            bool originalMotion = PresentationSettings.ReducedMotion;
            try
            {
                PresentationSettings.ReducedMotion = true;
                game.RestoreMatch(game.Setup, beforeClimb);
                Require(game.TryPlayerAction(route[route.Length - 1]), "Reduced-motion climb did not start");
                arrival = board.GetSlotView(new SlotId(upper, 3));
                bool reducedLanding = false;
                float deadlineForClimb = Time.realtimeSinceStartup + 10;
                while (!game.AcceptsBoardInput && Time.realtimeSinceStartup < deadlineForClimb)
                {
                    reducedLanding |= LandingVisible(arrival);
                    Require(!game.WaveAnimator.GetComponentsInChildren<Transform>().Any(t => t.name.StartsWith("WaveTile_")),
                        "Reduced motion spawned a flying tile");
                    yield return null;
                }
                Require(reducedLanding && !LandingVisible(arrival), "Reduced motion lost the static arrival frame");
                Require(game.AcceptsBoardInput && expected == JsonUtility.ToJson(MatchSnapshot.Capture(game)),
                    "Reduced-motion playback changed the settled terrace match");
            }
            finally { PresentationSettings.ReducedMotion = originalMotion; }
        }
        var pointer = new MouseState { position = new Vector2(camera.pixelWidth * .6f, camera.pixelHeight * .45f) };
        int turnBeforeOrbit = game.TurnNumber;
        InputSystem.QueueStateEvent(mouse, pointer.WithButton(MouseButton.Right));
        yield return Hold(2);
        for (int i = 0; i < 12; i++)
        {
            var drag = pointer.WithButton(MouseButton.Right).WithButton(MouseButton.Left);
            drag.delta = new Vector2(48f, 0);
            InputSystem.QueueStateEvent(mouse, drag);
            yield return null;
        }
        InputSystem.QueueStateEvent(mouse, pointer);
        yield return Hold(2);
        Require(Quaternion.Angle(camera.transform.rotation, initialView) > 100f, "Camera did not orbit.");
        Require(game.TurnNumber == turnBeforeOrbit, "Camera drag claimed a slot.");
        checkpoint("terrace-orbit");
        yield return Hold(record ? 14 : 1);

        var rear = BoardActions.LegalMoves(game.Board, game.ActivePlayer.Id).First(id =>
        {
            if (id.Side.LocalIndex != 4) return false;
            var view = board.GetSlotView(id);
            var pixel = camera.WorldToScreenPoint(view.transform.position);
            return camera.pixelRect.Contains(pixel) && !panels.Any(p => p.CoversScreenPoint(pixel)) &&
                Physics.Raycast(camera.ScreenPointToRay(pixel), out var hit) && hit.collider.GetComponentInParent<SlotView>() == view;
        });
        var rearPointer = new MouseState { position = camera.WorldToScreenPoint(board.GetSlotView(rear).transform.position) };
        InputSystem.QueueStateEvent(mouse, rearPointer);
        yield return Hold(2);
        InputSystem.QueueStateEvent(mouse, rearPointer.WithButton(MouseButton.Left));
        for (int i = 0; i < 12 && game.TurnNumber == turnBeforeOrbit; i++) yield return null;
        InputSystem.QueueStateEvent(mouse, rearPointer);
        Require(game.Board.GetSlot(rear).Owner == new PlayerId(2), "The rear riser was not clickable.");

        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Home));
            yield return Hold(2);
            Require(Vector3.Distance(initialPosition, camera.transform.position) < .01f, "Reset lost the fitted view.");
        }
        finally { InputSystem.RemoveDevice(keyboard); }
        while (game.WaveAnimator.IsPlaying) yield return null;
        var from = game.Board.Cells.Values.SelectMany(c => c.ExposedSides).SelectMany(s => s.Slots)
            .First(s => BoardActions.TryRelay(game.Board, s.Id, game.ActivePlayer.Id, out _, out _));
        game.ChooseRelay(true);
        game.OnSlotClicked(from.Id);
        checkpoint("relay");
        yield return Hold(record ? 18 : 1);
        int seat = game.ActivePlayer.Id.Value;
        Require(game.ConfirmRelay() && game.Roster.Players[seat - 1].RelaySpent, "Relay did not spend its charge.");
        yield return Hold(record ? 12 : 1);
        while (!game.AcceptsBoardInput) yield return null;
        checkpoint("supply");
        yield return Hold(record ? 14 : 1);
        int turn = game.TurnNumber;
        var caster = game.ActivePlayer.Id;
        Require(game.TakeSupply(MatchAction.Beacon) && game.TurnNumber == turn, "Supply must leave the turn open.");
        var allSlots = game.Board.Cells.Values.SelectMany(c => c.ExposedSides).SelectMany(s => s.Slots).ToList();
        var ice = allSlots.First(s => game.Abilities.CanTarget(MatchAction.Freeze, s.Id) &&
            game.Board.GetCell(s.Id.Side.Cell).Position.Y == 1);
        game.SelectAbility(MatchAction.Freeze);
        game.OnSlotClicked(ice.Id);
        Require(game.ConfirmAbility(), "Could not freeze a cube.");
        var rim = board.GetCellView(ice.Id.Side.Cell).transform.Find("Frost rim");
        Require(rim && rim.gameObject.activeInHierarchy, "Frozen cube is missing its rim");
        checkpoint("freeze");
        yield return Hold(record ? 16 : 1);
        var safe = allSlots.First(s => game.Abilities.CanClaim(s.Id) && game.Board.GetSide(s.Id.Side).Slots.All(t => t.IsNeutral));
        Require(game.TryPlayerAction(safe.Id), "Expected an unfrozen reply.");
        while (!game.AcceptsBoardInput) yield return null;
        Require(!game.Abilities.Frozen(ice.Id.Side.Cell) && !rim.gameObject.activeInHierarchy, "The ice did not thaw.");
        var marks = game.Board.Cells.Values.SelectMany(c => c.ExposedSides)
            .Where(f => f.SlotCount == 2 && f.Slots.All(s => game.Abilities.CanClaim(s.Id))).First().Slots.ToArray();
        game.SelectAbility(MatchAction.Beacon);
        game.OnSlotClicked(marks[0].Id);
        game.OnSlotClicked(marks[1].Id);
        Require(game.ConfirmAbility(), "Could not arm the Beacon.");
        checkpoint("beacon");
        yield return Hold(record ? 16 : 1);
        var replySlot = allSlots.First(s => s.Id.Side != marks[0].Id.Side && game.Abilities.CanClaim(s.Id) &&
            game.Board.GetSide(s.Id.Side).SlotCount > 2 && game.Board.GetSide(s.Id.Side).Slots.All(t => t.IsNeutral));
        Require(game.TryPlayerAction(replySlot.Id) && game.WaveAnimator.IsPlaying, "Beacon did not start the opening wave.");
        Require(!game.AcceptsBoardInput, "Beacon playback allowed another action.");
        checkpoint("beacon-wave");
        yield return Hold(record ? 12 : 1);
        float deadlineForBeacon = Time.realtimeSinceStartup + 12;
        while (!game.AcceptsBoardInput && !game.IsGameOver && Time.realtimeSinceStartup < deadlineForBeacon) yield return null;
        Require(game.AcceptsBoardInput && game.ActivePlayer.Id == caster,
            $"Beacon must return control to {caster}. Active {game.ActivePlayer.Id}, phase {game.CurrentPhase}, winner {game.Winner}.");
    }

    private static bool LandingVisible(SlotView slot)
    {
        var marker = slot.transform.Find("Wave landing");
        return marker && marker.GetComponent<Renderer>().enabled;
    }

    private static IEnumerator Hold(int frames)
    {
        for (int i = 0; i < frames; i++) yield return null;
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
