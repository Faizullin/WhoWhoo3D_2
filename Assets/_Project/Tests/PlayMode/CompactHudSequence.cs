using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Tests.PlayMode;

// Drives the same retained controls in the saved scene and the 800×480 standalone capture.
public static class CompactHudSequence
{
    public static IEnumerator Run(GameController game, BoardView board, Camera camera, Mouse mouse, Action capture)
    {
        game.StartMatch(new MatchSetup("terrace6x6", true, new SeatControl[4]));
        yield return null;
        yield return null;
        var canvas = game.GetComponentInChildren<Canvas>();
        var safe = canvas.transform.Find("Safe area");
        var header = (RectTransform)safe.Find("Turn and scores");
        var tray = (RectTransform)safe.Find("Ability tray");
        var panels = game.GetComponents<IBoardInputPanel>();
        bool Blocked(Vector2 p) => panels.Any(panel => panel.CoversScreenPoint(p));
        Vector2 Center(RectTransform rect) => rect.TransformPoint(rect.rect.center);
        var corners = new Vector3[4];
        foreach (var rect in new[] { header, tray })
        {
            rect.GetWorldCorners(corners);
            Require(corners.All(p => p.x >= Screen.safeArea.xMin && p.x <= Screen.safeArea.xMax &&
                p.y >= Screen.safeArea.yMin && p.y <= Screen.safeArea.yMax), "HUD outside safe area");
            Require(Blocked(Center(rect)), "HUD panel does not block the pointer");
        }
        for (int i = 1; i <= 4; i++) Require(header.Find($"Seat {i}").gameObject.activeInHierarchy, "Missing seat");
        Require(!Blocked(camera.pixelRect.center), "The HUD covers the board center");
        var freeze = (RectTransform)tray.Find("Freeze");
        var confirm = tray.Find("Confirm").GetComponent<Button>();
        var cancel = (RectTransform)tray.Find("Cancel");
        var initialView = camera.transform.rotation;
        var initialPosition = camera.transform.position;
        yield return Click(mouse, Center(freeze));
        Require(game.SelectedAction == MatchAction.Freeze && !game.Target.HasValue && game.History.Entries.Count == 0,
            "Clicking the Freeze card must only arm targeting");
        Require(!confirm.interactable, "Confirm enabled without a target");
        var point = VisibleTarget(game, board, camera, Blocked, out var target);
        yield return Click(mouse, point);
        Require(game.Target == target && confirm.interactable, "Board pointer did not preview the selected cube");
        Require(camera.transform.rotation == initialView && camera.transform.position == initialPosition,
            "Opening the tray moved the camera");
        capture?.Invoke();
        yield return null;
        var center = camera.pixelRect.center;
        var right = new MouseState { position = center }.WithButton(MouseButton.Right);
        InputSystem.QueueStateEvent(mouse, right);
        yield return null;
        right.delta = new Vector2(40, 0);
        InputSystem.QueueStateEvent(mouse, right);
        yield return null;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = center });
        yield return null;
        Require(Quaternion.Angle(camera.transform.rotation, initialView) > 3 && game.Target == target,
            "Orbit must keep the Freeze preview");
        yield return Click(mouse, Center(cancel));
        Require(game.SelectedAction == MatchAction.Claim && game.ActivePlayer.Inventory.freeze &&
            game.History.Entries.Count == 0, "Cancel consumed the ability or claimed a tile");

        yield return Click(mouse, Center(freeze));
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Escape));
            yield return null;
            yield return null;
            Require(game.SelectedAction == MatchAction.Claim, "Escape did not cancel targeting");
        }
        finally { InputSystem.RemoveDevice(keyboard); }

        yield return Click(mouse, Center(freeze));
        point = VisibleTarget(game, board, camera, Blocked, out target);
        yield return Click(mouse, point);
        yield return Click(mouse, Center((RectTransform)confirm.transform));
        Require(game.TurnNumber == 2 && !game.Roster.Players[0].Inventory.freeze &&
            game.Abilities.Frozen(target.Side.Cell) && game.History.Entries.Count == 1,
            "Confirm must freeze exactly one cube and spend one action");
    }

    private static Vector2 VisibleTarget(GameController game, BoardView board, Camera camera,
        Func<Vector2, bool> blocked, out SlotId target)
    {
        foreach (var cell in game.Board.Cells.Values.OrderByDescending(c => c.Position.Y))
        foreach (var face in cell.ExposedSides)
        foreach (var tile in face.Slots)
        {
            if (!game.Abilities.CanTarget(MatchAction.Freeze, tile.Id)) continue;
            var view = board.GetSlotView(tile.Id);
            var point = camera.WorldToScreenPoint(view.transform.position);
            if (point.z <= 0 || !camera.pixelRect.Contains(point) || blocked(point)) continue;
            if (!Physics.Raycast(camera.ScreenPointToRay(point), out var hit) ||
                hit.collider.GetComponentInParent<SlotView>() != view) continue;
            target = tile.Id;
            return point;
        }
        throw new InvalidOperationException("No visible legal Freeze target");
    }

    private static IEnumerator Click(Mouse mouse, Vector2 point)
    {
        var state = new MouseState { position = point };
        InputSystem.QueueStateEvent(mouse, state);
        yield return null;
        InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left));
        yield return null;
        InputSystem.QueueStateEvent(mouse, state.WithButton(MouseButton.Left, false));
        yield return null;
        yield return null;
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
