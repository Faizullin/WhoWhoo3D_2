using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board.View;

namespace WhoWhoo3D.MapEditor;

/// <summary>
/// Controls for <see cref="MapEditorController"/>: tool, player, filter, and save/load.
///
/// IMGUI, matching ScoreboardView and TimelineView — no Canvas or font import needed, and
/// replaced wholesale by the UI pass in docs/TODO.md §5.
/// </summary>
public sealed class MapEditorHud : MonoBehaviour
{
    [SerializeField] private MapEditorController editor;
    [SerializeField] private float width = 300f;
    [SerializeField] private Vector2 margin = new(20f, 20f);
    [Range(0.5f, 4f)]
    [SerializeField] private float uiScale = 1f;

    private const float ReferenceHeight = 900f;

    private GUIStyle _heading;
    private GUIStyle _label;
    private GUIStyle _note;
    private Texture2D _panel;

    private void Awake()
    {
        if (editor == null) editor = FindAnyObjectByType<MapEditorController>();
    }

    private void OnDestroy()
    {
        if (_panel != null) Destroy(_panel);
    }

    private void OnGUI()
    {
        if (editor == null) return;

        EnsureStyles();

        float scale = Mathf.Max(1f, Screen.height / ReferenceHeight) * uiScale;
        var previous = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

        const float height = 372f;
        GUILayout.BeginArea(new Rect(margin.x, margin.y, width, height), GUIContent.none);
        GUI.DrawTexture(new Rect(0, 0, width, height), _panel, ScaleMode.StretchToFill);

        GUILayout.Space(10f);
        GUILayout.Label("MAP EDITOR", _heading);

        DrawTools();
        DrawPainter();
        DrawShape();
        DrawFile();
        DrawStatus();

        GUILayout.EndArea();
        GUI.matrix = previous;
    }

    private void DrawTools()
    {
        GUILayout.Label("Tool", _note);
        GUILayout.BeginHorizontal();
        GUILayout.Space(12f);
        foreach (MapEditorController.Tool t in System.Enum.GetValues(typeof(MapEditorController.Tool)))
        {
            bool on = editor.ActiveTool == t;
            GUI.backgroundColor = on ? new Color(0.35f, 0.70f, 1f) : Color.white;
            if (GUILayout.Button(t.ToString(), GUILayout.Height(28f)))
                editor.ActiveTool = t;
        }
        GUI.backgroundColor = Color.white;
        GUILayout.Space(12f);
        GUILayout.EndHorizontal();
    }

    private void DrawPainter()
    {
        GUILayout.Space(4f);
        GUILayout.Label("Player (Paint tool)", _note);
        GUILayout.BeginHorizontal();
        GUILayout.Space(12f);
        for (int p = 1; p <= 4; p++)
        {
            GUI.backgroundColor = editor.PaintPlayer == p ? SlotView.ColorFor(new PlayerId(p)) : Color.white;
            if (GUILayout.Button($"P{p}", GUILayout.Height(26f)))
                editor.PaintPlayer = p;
        }
        GUI.backgroundColor = Color.white;
        GUILayout.Space(12f);
        GUILayout.EndHorizontal();
        GUILayout.Label("Click a tile again to clear it.", _note);
    }

    private void DrawShape()
    {
        GUILayout.Space(4f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(12f);
        bool top = GUILayout.Toggle(editor.TopFaceOnly, " Top face only", GUILayout.Height(22f));
        if (top != editor.TopFaceOnly) editor.TopFaceOnly = top;
        GUILayout.Space(12f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Space(12f);
        GUILayout.Label("New slab:", _label, GUILayout.Width(74f));
        foreach (int n in new[] { 3, 4, 5, 6 })
            if (GUILayout.Button(n.ToString(), GUILayout.Height(24f)))
                editor.NewSlab(n);
        GUILayout.Space(12f);
        GUILayout.EndHorizontal();
    }

    private void DrawFile()
    {
        GUILayout.Space(6f);
        GUILayout.BeginHorizontal();
        GUILayout.Space(12f);
        GUILayout.Label("Name", _label, GUILayout.Width(48f));
        editor.MapFileName = GUILayout.TextField(editor.MapFileName, GUILayout.Height(22f));
        GUILayout.Space(12f);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Space(12f);
        if (GUILayout.Button("Load", GUILayout.Height(28f))) editor.Load();

        // Saving an invalid map produces a file that cannot be loaded back, so the button
        // is disabled rather than allowed to fail.
        bool wasEnabled = GUI.enabled;
        GUI.enabled = wasEnabled && editor.IsValid;
        GUI.backgroundColor = editor.IsValid ? new Color(0.35f, 0.75f, 0.45f) : Color.white;
        if (GUILayout.Button("Save", GUILayout.Height(28f))) editor.Save();
        GUI.backgroundColor = Color.white;
        GUI.enabled = wasEnabled;

        GUILayout.Space(12f);
        GUILayout.EndHorizontal();
    }

    private void DrawStatus()
    {
        GUILayout.Space(6f);
        GUILayout.Label($"{editor.CubeCount} cubes", _note);

        if (!editor.IsValid)
        {
            _note.normal.textColor = new Color(1f, 0.55f, 0.5f);
            GUILayout.Label(editor.Errors[0], _note);
            _note.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        }

        if (!string.IsNullOrEmpty(editor.Status))
            GUILayout.Label(editor.Status, _note);
    }

    private void EnsureStyles()
    {
        if (_panel != null) return;

        _panel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        _panel.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.10f, 0.88f));
        _panel.Apply();

        _heading = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16, fontStyle = FontStyle.Bold, padding = new RectOffset(14, 14, 0, 4),
        };
        _heading.normal.textColor = Color.white;

        _label = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleLeft };
        _label.normal.textColor = Color.white;

        _note = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12, wordWrap = true, padding = new RectOffset(14, 14, 0, 0),
        };
        _note.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
    }
}
