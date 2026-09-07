using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Turn;

namespace WhoWhoo3D.UI.HUD;

/// <summary>
/// Top-corner scoreboard: every player, their tile count, and whose turn it is.
///
/// Drawn with IMGUI so it needs no Canvas, prefab or font import — drop the component
/// on any GameObject and it works. That is deliberate for playtesting; the UI pass in
/// docs/TODO.md §5 replaces it with uGUI, at which point this can be deleted.
/// </summary>
public sealed class ScoreboardView : MonoBehaviour
{
    [SerializeField] private GameController gameController;

    [Header("Layout")]
    [SerializeField] private Corner corner = Corner.TopLeft;
    [SerializeField] private Vector2 margin = new(20f, 20f);
    [SerializeField] private float width = 320f;

    [Tooltip("Extra multiplier on top of the automatic resolution scaling.")]
    [Range(0.5f, 4f)]
    [SerializeField] private float uiScale = 1f;

    /// <summary>
    /// Layout is authored against this height and scaled to whatever the screen
    /// actually is, so the panel keeps its proportions on a 4K or Retina display
    /// instead of shrinking into the corner.
    /// </summary>
    private const float ReferenceHeight = 900f;

    private const float RowHeight = 30f;
    private const float SwatchSize = 18f;

    public enum Corner { TopLeft, TopRight }

    private GUIStyle _heading;
    private GUIStyle _name;
    private GUIStyle _score;
    private GUIStyle _note;
    private GUIStyle _row;
    private GUIStyle _activeRow;

    private Texture2D _panelTex;
    private Texture2D _activeRowTex;

    private void Awake()
    {
        if (gameController == null)
            gameController = FindAnyObjectByType<GameController>();
    }

    private void OnDestroy()
    {
        if (_panelTex != null) Destroy(_panelTex);
        if (_activeRowTex != null) Destroy(_activeRowTex);
    }

    private void OnGUI()
    {
        if (gameController == null || gameController.Roster == null)
            return;

        EnsureStyles();

        // IMGUI works in raw pixels, so on a high-DPI screen everything renders tiny.
        // Scale the whole GUI and lay out in logical units instead.
        float scale = Mathf.Max(1f, Screen.height / ReferenceHeight) * uiScale;
        var previousMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

        float logicalWidth = Screen.width / scale;
        int players = gameController.Roster.Players.Count;
        float height = 34f + players * RowHeight + 54f;
        float x = corner == Corner.TopLeft ? margin.x : logicalWidth - width - margin.x;

        GUILayout.BeginArea(new Rect(x, margin.y, width, height), GUIContent.none);
        GUI.DrawTexture(new Rect(0, 0, width, height), _panelTex, ScaleMode.StretchToFill);

        GUILayout.Space(8f);
        GUILayout.Label("PLAYERS", _heading);
        DrawPlayers();
        GUILayout.Space(6f);
        GUILayout.Label(StatusLine(), _note);

        GUILayout.EndArea();
        GUI.matrix = previousMatrix;
    }

    private void DrawPlayers()
    {
        var active = gameController.ActivePlayer;

        foreach (var player in gameController.Roster.Players)
        {
            bool isActive = !gameController.IsGameOver && player == active;

            // Read the DRAWN state, not the model: the simulator finishes the whole
            // wave before the first frame animates, so the model would show the
            // outcome while the board is still mid-chain.
            int score = gameController.DisplayScoreFor(player.Id);
            bool isOut = gameController.DisplayIsOut(player);

            // The row background comes from the STYLE, not from a reserved rect.
            // GUILayoutUtility.GetRect returns a zero rect during the Layout pass, so
            // positioning a row from it stacks every row at y = 0 on top of the header.
            GUILayout.BeginHorizontal(isActive ? _activeRow : _row, GUILayout.Height(RowHeight));

            var swatch = GUILayoutUtility.GetRect(
                SwatchSize, SwatchSize, GUILayout.Width(SwatchSize), GUILayout.Height(SwatchSize));
            if (Event.current.type == EventType.Repaint)
            {
                swatch.y += (RowHeight - SwatchSize) * 0.5f;
                GUI.DrawTexture(swatch, Texture2D.whiteTexture, ScaleMode.StretchToFill,
                    false, 0f, SlotView.ColorFor(player.Id), 0f, 3f);
            }

            GUILayout.Space(10f);

            _name.normal.textColor = isOut ? new Color(1f, 1f, 1f, 0.35f) : Color.white;

            string label = $"P{player.Id.Value}";
            if (isActive && !isOut) label = "▶ " + label;
            if (isOut) label += "  (out)";

            GUILayout.Label(label, _name);
            GUILayout.FlexibleSpace();
            GUILayout.Label(score.ToString(), _score, GUILayout.Width(46f));

            GUILayout.EndHorizontal();
        }
    }

    private string StatusLine()
    {
        if (gameController.IsGameOver)
            return gameController.Winner.IsNone
                ? "Game over — no winner"
                : $"Game over — P{gameController.Winner.Value} wins";

        if (gameController.CurrentPhase is BoardWavePhase)
            return "Expanding…";

        return $"P{gameController.ActivePlayer.Id.Value} to play — click a white tile";
    }

    private void EnsureStyles()
    {
        if (_panelTex != null) return;

        _panelTex = SolidTexture(new Color(0.07f, 0.08f, 0.10f, 0.86f));
        _activeRowTex = SolidTexture(new Color(1f, 1f, 1f, 0.10f));

        _heading = new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            padding = new RectOffset(14, 14, 0, 2),
        };
        _heading.normal.textColor = new Color(1f, 1f, 1f, 0.6f);

        _name = new GUIStyle(GUI.skin.label)
        {
            fontSize = 19,
            alignment = TextAnchor.MiddleLeft,
        };
        _name.normal.textColor = Color.white;

        _score = new GUIStyle(_name)
        {
            alignment = TextAnchor.MiddleRight,
            fontStyle = FontStyle.Bold,
        };

        _note = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            wordWrap = true,
            padding = new RectOffset(14, 14, 0, 0),
        };
        _note.normal.textColor = new Color(1f, 1f, 1f, 0.7f);

        // Padding on the row style is what insets the contents; the background is what
        // marks the active player.
        _row = new GUIStyle { padding = new RectOffset(14, 14, 0, 0) };
        _activeRow = new GUIStyle(_row);
        _activeRow.normal.background = _activeRowTex;
    }

    private static Texture2D SolidTexture(Color color)
    {
        var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }
}

