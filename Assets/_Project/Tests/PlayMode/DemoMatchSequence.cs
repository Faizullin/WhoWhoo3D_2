using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using WhoWhoo3D.Domain.Board;
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

        game.StartLocalMatch("demo4x4", 3);
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
        game.RestartMatch();
        yield return null;
        Require(game.TurnNumber == 1 && !game.IsGameOver && game.History.Entries.Count == 0 &&
            game.ActivePlayer.Id == new PlayerId(1) && opening.All(pair => game.Board.GetSlot(pair.Key)?.Owner == pair.Value),
            "Restart did not restore the opening position.");
        checkpoint("restart");
        yield return Hold(record ? 14 : 1);
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
