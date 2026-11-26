using UnityEngine;

namespace WhoWhoo3D.UI;

/// <summary>Maps the screen safe area into top-left IMGUI coordinates for drawing and input.</summary>
public readonly struct HudViewport
{
    private readonly Vector2 _origin;
    public readonly float Scale;
    public readonly Vector2 Size;

    public HudViewport(Rect safeArea, float screenHeight, float uiScale)
    {
        _origin = new Vector2(safeArea.xMin, screenHeight - safeArea.yMax);
        float preferred = Mathf.Max(1f, safeArea.height / 900f) * uiScale;
        // Keep the four-seat setup and its room controls reachable at large text scales.
        Scale = Mathf.Max(.01f, Mathf.Min(preferred, Mathf.Min(safeArea.width / 360f, safeArea.height / 410f)));
        Size = safeArea.size / Scale;
    }

    public Matrix4x4 Matrix => Matrix4x4.TRS(_origin, Quaternion.identity, Vector3.one * Scale);
    public Rect Pixels(Rect area) => new(_origin + area.position * Scale, area.size * Scale);
}
