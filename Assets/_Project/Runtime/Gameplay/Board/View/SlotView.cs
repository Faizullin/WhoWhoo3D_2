using UnityEngine;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Board.View
{
    /// <summary>
    /// One tile: a thin square slab lying on its face, near the edge it belongs to.
    /// Carries the collider the player clicks, so input resolves to an exact slot rather
    /// than to a whole cube.
    /// </summary>
    public sealed class SlotView : MonoBehaviour
    {
        // One material for every tile; colour comes from a per-renderer property block, so
        // a tile can be tinted or blended without instantiating materials.
        private static Material _sharedMaterial;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private float size = 0.30f;
        [SerializeField] private float thickness = 0.05f;

        private Slot _slot;
        private Renderer _renderer;
        private MaterialPropertyBlock _block;

        /// <summary>Presentation override used by WaveAnimator; null means "follow the model".</summary>
        private PlayerId? _displayOverride;

        public SlotId SlotId { get; private set; }

        public void Init(Slot slot, int face, int edge)
        {
            _slot = slot;
            SlotId = slot.Id;
            name = $"Slot_{face}_{edge}";

            transform.localPosition = CubeGeometry.SlotLocalPosition(face, edge);
            transform.localRotation = CubeGeometry.SlotLocalRotation(face, edge);
            transform.localScale = new Vector3(size, thickness, size);

            var visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.transform.SetParent(transform, false);
            _renderer = visual.GetComponent<Renderer>();
            _renderer.sharedMaterial = SharedMaterial();

            // The primitive's own collider is what the raycast hits; route it back here.
            var collider = visual.GetComponent<BoxCollider>();
            if (collider != null)
                collider.isTrigger = false;

            Refresh();
        }

        public void ShowOwner(PlayerId owner)
        {
            _displayOverride = owner;
            Render(owner);
        }

        public void ClearOverride()
        {
            _displayOverride = null;
            Refresh();
        }

        public void Refresh() => Render(_displayOverride ?? _slot.Owner);

        /// <summary>
        /// Draws an arbitrary colour. Used to fade a tile toward white while the tile it
        /// spent is still in flight, so the source empties as the sphere travels rather
        /// than snapping when it lands.
        /// </summary>
        public void ShowColor(Color color)
        {
            if (_renderer == null) return;

            _block ??= new MaterialPropertyBlock();
            _renderer.GetPropertyBlock(_block);
            _block.SetColor(BaseColorId, color);
            _block.SetColor(ColorId, color);
            _renderer.SetPropertyBlock(_block);
        }

        private void Render(PlayerId owner) => ShowColor(ColorFor(owner));

        /// <summary>The one material every tile shares. Colour comes from a property block.</summary>
        public static Material SharedMaterial() =>
            _sharedMaterial ??= new Material(ResolveShader()) { color = Color.white };

        /// <summary>
        /// Shader.Find only sees shaders included in the build. A scene that spawns every
        /// tile from code can ship without a URP material, so fall back rather than hand
        /// back null and render everything magenta.
        /// </summary>
        private static Shader ResolveShader()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader != null) return shader;

            shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader != null) return shader;

            Debug.LogWarning(
                "URP shaders not found — falling back to Sprites/Default. Add " +
                "'Universal Render Pipeline/Lit' to Always Included Shaders for player builds.");
            return Shader.Find("Sprites/Default");
        }

        /// <summary>The colour a player's tiles are drawn in. Shared with the HUD.</summary>
        public static Color ColorFor(PlayerId id)
        {
            int index = id.Value;
            if (index < 0 || index > 4) index = 0;
            return OwnerColor(index);
        }

        /// <summary>Colour of an unclaimed tile — what a spent tile fades toward.</summary>
        public static Color NeutralColor => OwnerColor(0);

        private static Color OwnerColor(int index)
        {
            switch (index)
            {
                case 1: return new Color(0.85f, 0.25f, 0.22f); // red
                case 2: return new Color(0.20f, 0.30f, 0.90f); // blue
                case 3: return new Color(0.25f, 0.70f, 0.30f); // green
                case 4: return new Color(0.95f, 0.80f, 0.20f); // yellow
                default: return new Color(0.96f, 0.96f, 0.96f); // unclaimed
            }
        }
    }
}
