using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Input
{
    /// <summary>
    /// Captures player click/tap input via the New Input System, performs a raycast
    /// to identify the target CellView, and forwards the event to GameController.
    /// </summary>
    public class PlayerInputHandler : MonoBehaviour
    {
        [SerializeField] private GameController gameController;
        [SerializeField] private Camera inputCamera;
        [SerializeField] private InputActionProperty clickAction;

        public event Action<SlotId> SlotClicked;

        private void Awake()
        {
            if (inputCamera == null)
                inputCamera = Camera.main;

            if (gameController == null)
                gameController = FindAnyObjectByType<GameController>();
        }

        private void OnEnable()
        {
            if (clickAction.action != null)
                clickAction.action.Enable();
        }

        private void OnDisable()
        {
            if (clickAction.action != null)
                clickAction.action.Disable();
        }

        private SlotView _hovered;
        private IBoardInputPanel[] _panels;

        private BoardCamera _rig;
        private SlotView _partner;
        private bool _dragging, _keepFocus;
        private Vector2 _focusPosition;

        private void Start()
        {
            _panels = GetComponents<IBoardInputPanel>();
            if (inputCamera != null) _rig = new BoardCamera(inputCamera);
        }

        private bool Navigate(Vector2 position, bool overPanel)
        {
            var mouse = Mouse.current;
            bool navigating = mouse != null && (mouse.rightButton.isPressed || mouse.middleButton.isPressed);
            if (navigating && (mouse.rightButton.wasPressedThisFrame || mouse.middleButton.wasPressedThisFrame))
                _dragging = !overPanel;
            if (!navigating) _dragging = false;
            if (_dragging)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (mouse.rightButton.isPressed) _rig.Orbit(delta.x * .3f, -delta.y * .3f);
                else _rig.Pan(delta);
            }
            var keys = Keyboard.current;
            bool keyboardMoved = false;
            if (keys != null && !gameController.InSetup)
            {
                float yaw = keys.dKey.ReadValue() - keys.aKey.ReadValue();
                float pitch = keys.wKey.ReadValue() - keys.sKey.ReadValue();
                float zoom = keys.eKey.ReadValue() - keys.qKey.ReadValue();
                float dt = Time.unscaledDeltaTime;
                if (yaw != 0 || pitch != 0) _rig.Orbit(yaw * 95 * dt, pitch * 65 * dt);
                if (zoom != 0) _rig.Zoom(zoom * 8 * dt);
                if (keys.homeKey.wasPressedThisFrame) _rig.Reset();
                keyboardMoved = yaw != 0 || pitch != 0 || zoom != 0 || keys.homeKey.wasPressedThisFrame;
            }
            float scroll = mouse != null && !overPanel ? mouse.scroll.ReadValue().y : 0f;
            if (scroll != 0) _rig.Zoom(Mathf.Sign(scroll));
            if (_dragging || keyboardMoved || scroll != 0)
            {
                _keepFocus = true;
                _focusPosition = position;
            }
            return navigating || keyboardMoved || scroll != 0;
        }

        private void OnGUI()
        {
            if (!inputCamera || !gameController || !gameController.RenderedBoard ||
                gameController.InSetup || gameController.IsReviewingHistory || Event.current.type != EventType.Repaint) return;
            foreach (var seat in gameController.Roster.Players)
            {
                if (!seat.Inventory.HasBeacon) continue;
                var color = SlotView.ColorFor(seat.Id);
                Mark(gameController.RenderedBoard.GetSlotView(seat.Inventory.BeaconFirst), true, color);
                Mark(gameController.RenderedBoard.GetSlotView(seat.Inventory.BeaconSecond), true, color);
            }
            if (!gameController.AcceptsBoardInput) return;
            if (gameController.SelectedAction == MatchAction.Freeze && gameController.Target is SlotId frost)
            {
                foreach (var side in gameController.Board.GetCell(frost.Side.Cell).ExposedSides)
                    foreach (var tile in side.Slots) Mark(gameController.RenderedBoard.GetSlotView(tile.Id), false, Color.cyan);
                return;
            }
            if (gameController.SelectedAction == MatchAction.Beacon)
            {
                if (gameController.Target is SlotId a) Mark(gameController.RenderedBoard.GetSlotView(a), true);
                if (gameController.SecondTarget is SlotId b) Mark(gameController.RenderedBoard.GetSlotView(b), true);
                return;
            }
            if (gameController.ChoosingRelay && gameController.RelaySource is SlotId from &&
                gameController.Abilities.TryRelay(from, out var to, out var bonus))
            {
                Mark(gameController.RenderedBoard.GetSlotView(from), true);
                Mark(gameController.RenderedBoard.GetSlotView(to));
                Mark(gameController.RenderedBoard.GetSlotView(bonus));
                return;
            }
            if (_hovered && _hovered.IsClaimHighlighted && _partner) Mark(_partner);
        }

        private void Mark(SlotView view, bool source = false, Color? color = null)
        {
            if (view == null) return;
            // Project the marker even when the destination sits behind a riser.
            var point = inputCamera.WorldToScreenPoint(view.transform.position);
            if (point.z <= 0) return;
            float x = Mathf.Clamp(point.x, 18, Screen.width - 18);
            float y = Mathf.Clamp(Screen.height - point.y, 18, Screen.height - 18);
            var saved = GUI.color;
            GUI.color = color ?? new Color(1f, .78f, .18f);
            var ink = Texture2D.whiteTexture;
            if (source)
            {
                GUI.DrawTexture(new Rect(x - 8, y - 8, 16, 2), ink);
                GUI.DrawTexture(new Rect(x - 8, y + 6, 16, 2), ink);
                GUI.DrawTexture(new Rect(x - 8, y - 6, 2, 12), ink);
                GUI.DrawTexture(new Rect(x + 6, y - 6, 2, 12), ink);
            }
            else
            {
                GUI.DrawTexture(new Rect(x - 6, y - 1, 12, 2), ink);
                GUI.DrawTexture(new Rect(x - 1, y - 6, 2, 12), ink);
            }
            GUI.color = saved;
        }

        private void Update()
        {
            bool clicked = WasClicked(out var clickPosition);
            Vector2 position = clicked ? clickPosition : GetPointerPosition();
            bool overPanel = OverPanel(position);
            if (_rig != null && gameController != null)
            {
                _rig.Frame(gameController.Board);
                if (!gameController.InSetup && Navigate(position, overPanel)) return;
            }
            if (_keepFocus && !clicked && !overPanel && gameController != null && gameController.AcceptsBoardInput &&
                (position - _focusPosition).sqrMagnitude < 1f) return;
            _keepFocus = false;
            bool enabled = gameController == null || gameController.AcceptsBoardInput;
            var slot = enabled && !overPanel ? PickSlot(position) : null;
            if (_hovered != slot && _hovered != null) _hovered.SetHovered(false);
            _hovered = slot;
            _partner = slot != null && gameController != null && gameController.RenderedBoard != null && slot.IsClaimHighlighted
                ? gameController.RenderedBoard.GetSlotView(slot.Partner) : null;
            if (slot == null) return;

            slot.SetHovered(true);
            if (clicked)
            {
                SlotClicked?.Invoke(slot.SlotId);
                gameController?.OnSlotClicked(slot.SlotId);
            }
        }

        private bool OverPanel(Vector2 position)
        {
            foreach (var panel in _panels)
                if (panel.CoversScreenPoint(position)) return true;
            return false;
        }

        private bool WasClicked(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;

            // A configured action is authoritative: falling through to the raw-device
            // checks would let every click past regardless of its bindings, making the
            // field decorative. But an action with NO bindings can never fire, so treat
            // it as unconfigured rather than letting it silently swallow all input --
            // that is exactly what an inline action left half-set-up in the inspector
            // looks like.
            var action = clickAction.action;
            if (action != null && action.bindings.Count > 0)
            {
                if (!action.WasPressedThisFrame())
                    return false;

                screenPosition = GetPointerPosition();
                return true;
            }

            // No action configured: read the pointer directly so the game is playable
            // straight out of the scene. Pointer covers mouse, touchscreen and pen.
            if (Pointer.current != null && Pointer.current.press.wasPressedThisFrame)
            {
                screenPosition = Pointer.current.position.ReadValue();
                return true;
            }

            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }

            return false;
        }

        private static Vector2 GetPointerPosition()
        {
            if (Pointer.current != null)
                return Pointer.current.position.ReadValue();
            if (Mouse.current != null)
                return Mouse.current.position.ReadValue();
            return Vector2.zero;
        }

        private SlotView PickSlot(Vector2 screenPosition)
        {
            if (inputCamera == null) inputCamera = Camera.main;
            if (inputCamera == null) return null;
            var ray = inputCamera.ScreenPointToRay(screenPosition);
            return Physics.Raycast(ray, out var hit) ? hit.collider.GetComponentInParent<SlotView>() : null;
        }
    }
}
