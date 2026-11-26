using System;
using UnityEngine;
using UnityEngine.InputSystem;
using WhoWhoo3D.Domain.Board;
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

        private void Start() => _panels = GetComponents<IBoardInputPanel>();

        private void Update()
        {
            bool clicked = WasClicked(out var clickPosition);
            Vector2 position = clicked ? clickPosition : GetPointerPosition();
            bool enabled = gameController == null || gameController.AcceptsBoardInput;
            var slot = enabled && !OverPanel(position) ? PickSlot(position) : null;
            if (_hovered != slot && _hovered != null) _hovered.SetHovered(false);
            _hovered = slot;
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
