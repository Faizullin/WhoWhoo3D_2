using UnityEngine;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Board.View;

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

    private bool _locked, _relayHint;
    private GameObject _hoverOutline;
    private MeshFilter _stamp;
    public bool IsClaimHighlighted { get; private set; }

    public void SetTurnHint(bool legal, bool locked, bool relayHint = false)
    {
        if (IsClaimHighlighted == legal && _locked == locked && _relayHint == relayHint) return;
        _relayHint = relayHint;
        IsClaimHighlighted = legal;
        _locked = locked;
        SetHovered(false);
        Refresh();
    }

    public void SetHovered(bool hovered)
    {
        bool show = (hovered && IsClaimHighlighted) || _relayHint;
        if (show && _hoverOutline == null)
        {
            _hoverOutline = new GameObject("Hover outline");
            _hoverOutline.transform.SetParent(transform, false);
            _hoverOutline.transform.localPosition = Vector3.down * 0.16f;
            _hoverOutline.transform.localScale = new Vector3(1.28f, 0.6f, 1.28f);
            _hoverOutline.AddComponent<MeshFilter>().sharedMesh = _renderer.GetComponent<MeshFilter>().sharedMesh;
            var rim = _hoverOutline.AddComponent<MeshRenderer>();
            rim.sharedMaterial = SharedMaterial();
            var gold = new MaterialPropertyBlock();
            gold.SetColor(BaseColorId, new Color(1f, 0.7f, 0.08f));
            gold.SetColor(ColorId, new Color(1f, 0.7f, 0.08f));
            rim.SetPropertyBlock(gold);
        }
        if (_hoverOutline != null && _hoverOutline.activeSelf != show)
            _hoverOutline.SetActive(show);
    }

    private Renderer _landing;

    public void ShowLanding(bool show)
    {
        if (show && _landing == null)
            _landing = BoardMeshes.Frame(transform, "Wave landing", Vector3.up * .56f,
                Quaternion.identity, 1.26f, new Color32(255, 247, 202, 255));
        if (_landing != null) _landing.enabled = show;
    }

    public SlotId SlotId { get; private set; }
    public SlotId Partner => _slot.Partner;

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
        visual.GetComponent<MeshFilter>().sharedMesh = BoardMeshes.Cube;
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

    private void Render(PlayerId owner)
    {
        if (!owner.IsNone && _stamp == null)
        {
            var go = new GameObject("Seat pips", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.up * .52f;
            _stamp = go.GetComponent<MeshFilter>();
            var ink = go.GetComponent<MeshRenderer>();
            ink.sharedMaterial = SharedMaterial();
            var block = new MaterialPropertyBlock();
            Color dark = new Color32(24, 33, 44, 255);
            block.SetColor(BaseColorId, dark);
            block.SetColor(ColorId, dark);
            ink.SetPropertyBlock(block);
        }
        if (_stamp != null)
        {
            _stamp.gameObject.SetActive(!owner.IsNone);
            if (!owner.IsNone) _stamp.sharedMesh = BoardMeshes.SeatStamp(owner.Value);
        }
        var color = ColorFor(owner);
        if (!_displayOverride.HasValue && owner.IsNone)
        {
            if (IsClaimHighlighted) color = new Color32(142, 214, 199, 255);
            else if (_locked) color = new Color32(91, 105, 119, 255);
        }
        ShowColor(color);
    }

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
            case 1: return new Color32(241, 109, 92, 255); // coral
            case 2: return new Color32(95, 153, 247, 255); // blue
            case 3: return new Color32(193, 138, 231, 255); // violet
            case 4: return new Color32(249, 194, 80, 255); // amber
            default: return new Color32(211, 222, 226, 255); // unclaimed
        }
    }
}

