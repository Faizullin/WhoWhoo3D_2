using UnityEngine.EventSystems;
using UnityEngine.UI;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.UI.HUD
{
    internal sealed class HudButton : Button
    {
        public bool Hovered { get; private set; }

        public override void OnPointerEnter(PointerEventData e) { Hovered = true; base.OnPointerEnter(e); }
        public override void OnPointerExit(PointerEventData e) { Hovered = false; base.OnPointerExit(e); }
        protected override void OnDisable() { Hovered = false; base.OnDisable(); }

        protected override void DoStateTransition(SelectionState state, bool instant) =>
            base.DoStateTransition(state, instant || PresentationSettings.ReducedMotion);
    }
}
