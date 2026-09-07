using System.Text;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Turn;

namespace WhoWhoo3D.UI.HUD
{
    /// <summary>
    /// Top-corner scoreboard: every player, their tile count, and whose turn it is.
    ///
    /// Drawn with IMGUI so it needs no Canvas, prefab or font import — drop the component
    /// on any GameObject and it works. That is deliberate for playtesting; Stage 9 replaces
    /// it with proper uGUI/TextMeshPro, at which point this can be deleted.
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

        public enum Corner { TopLeft, TopRight }

        private const float RowHeight = 30f;

        private GUIStyle _heading;
        private GUIStyle _row;
        private GUIStyle _score;
        private GUIStyle _note;
        private Texture2D _panel;

        private void Awake()
        {
            if (gameController == null)
                gameController = FindAnyObjectByType<GameController>();
        }

        private void OnDestroy()
        {
            if (_panel != null) Destroy(_panel);
        }

        private void OnGUI()
        {
            if (gameController == null || gameController.Roster == null)
                return;

            EnsureStyles();

            // IMGUI works in raw pixels, so on a high-DPI screen everything renders tiny.
            // Scale the whole GUI and lay out in logical units instead.
            float scale = Mathf.Max(1f, Screen.height / ReferenceHeight) * uiScale;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

            float logicalWidth = Screen.width / scale;
            int players = gameController.Roster.Players.Count;
            float height = 72f + players * RowHeight + 34f;
            float x = corner == Corner.TopLeft ? margin.x : logicalWidth - width - margin.x;

            GUILayout.BeginArea(new Rect(x, margin.y, width, height), GUIContent.none);
            GUI.DrawTexture(new Rect(0, 0, width, height), _panel, ScaleMode.StretchToFill);

            GUILayout.Space(10f);
            GUILayout.Label("  PLAYERS", _heading);
            DrawPlayers();
            GUILayout.Space(6f);
            GUILayout.Label(StatusLine(), _note);

            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private void DrawPlayers()
        {
            var board = gameController.Board;
            var active = gameController.ActivePlayer;

            foreach (var player in gameController.Roster.Players)
            {
                bool isActive = !gameController.IsGameOver && player == active;

                var rowRect = GUILayoutUtility.GetRect(width, RowHeight);
                if (isActive)
                {
                    // A glyph alone is easy to miss; band the whole row instead.
                    GUI.DrawTexture(new Rect(0f, rowRect.y, width, RowHeight),
                        Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                        new Color(1f, 1f, 1f, 0.10f), 0f, 0f);
                }

                GUILayout.BeginArea(new Rect(0f, rowRect.y, width, RowHeight));
                GUILayout.BeginHorizontal(GUILayout.Height(RowHeight));
                GUILayout.Space(12f);

                // Colour swatch, so the label maps to what is on the board.
                var swatch = GUILayoutUtility.GetRect(20f, 20f, GUILayout.Width(20f), GUILayout.Height(20f));
                swatch.y += 5f;
                GUI.DrawTexture(swatch, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0f,
                    SlotView.ColorFor(player.Id), 0f, 2f);

                _row.normal.textColor = player.IsAlive ? Color.white : new Color(1f, 1f, 1f, 0.35f);

                var label = new StringBuilder();
                label.Append(isActive ? " ▶ " : "   ");
                label.Append($"P{player.Id.Value}");
                if (!player.IsAlive) label.Append("  (out)");

                GUILayout.Label(label.ToString(), _row, GUILayout.Width(150f));
                GUILayout.FlexibleSpace();
                GUILayout.Label(player.ComputeScore(board).ToString(), _score, GUILayout.Width(52f));
                GUILayout.Space(12f);
                GUILayout.EndHorizontal();
                GUILayout.EndArea();
            }
        }

        private string StatusLine()
        {
            if (gameController.IsGameOver)
                return gameController.Winner.IsNone
                    ? "  Game over — no winner"
                    : $"  Game over — P{gameController.Winner.Value} wins";

            if (gameController.CurrentPhase is BoardWavePhase)
                return "  Expanding…";

            return $"  P{gameController.ActivePlayer.Id.Value} to play — click a white tile";
        }

        private void EnsureStyles()
        {
            if (_panel != null) return;

            _panel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _panel.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.10f, 0.82f));
            _panel.Apply();

            _heading = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
            _heading.normal.textColor = new Color(1f, 1f, 1f, 0.6f);

            _row = new GUIStyle(GUI.skin.label) { fontSize = 20 };
            _row.normal.textColor = Color.white;

            _score = new GUIStyle(_row) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };

            _note = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _note.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
        }
    }
}
