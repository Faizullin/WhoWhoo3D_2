using System;
using UnityEngine;
using WhoWhoo3D.Gameplay.Maps;
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
    /// on any GameObject and it works. That is deliberate for playtesting; the UI pass in
    /// docs/TODO.md §5 replaces it with uGUI, at which point this can be deleted.
    /// </summary>
    public sealed class ScoreboardView : MonoBehaviour, IBoardInputPanel
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

        private string[] _maps;
        private int _mapIndex;
        private int _players;
        private string _setupError;
        private Rect _panelPixels;
        private GUIStyle _title;

        public bool CoversScreenPoint(Vector2 point) =>
            isActiveAndEnabled && _panelPixels.Contains(new Vector2(point.x, Screen.height - point.y));

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
            float logicalHeight = Screen.height / scale;
            if (gameController.InSetup)
            {
                DrawSetup(logicalWidth, logicalHeight, scale);
                GUI.matrix = previousMatrix;
                return;
            }
            int players = gameController.Roster.Players.Count;
            float height = 34f + players * RowHeight + (gameController.IsGameOver ? 103f : 54f);
            float x = corner == Corner.TopLeft ? margin.x : logicalWidth - width - margin.x;

            var panel = new Rect(x, margin.y, width, height);
            _panelPixels = new Rect(panel.position * scale, panel.size * scale);
            GUILayout.BeginArea(panel, GUIContent.none);
            GUI.DrawTexture(new Rect(0, 0, width, height), _panelTex, ScaleMode.StretchToFill);

            GUILayout.Space(8f);
            GUILayout.Label(gameController.IsGameOver ? "MATCH RESULT" : $"TURN {gameController.TurnNumber}", _heading);
            DrawPlayers();
            GUILayout.Space(6f);
            GUILayout.Label(StatusLine(), _note);

            if (gameController.IsGameOver)
            {
                GUILayout.Space(6);
                GUILayout.BeginHorizontal();
                GUILayout.Space(12);
                if (GUILayout.Button("Play again", GUILayout.Height(32))) gameController.RestartMatch();
                if (GUILayout.Button("Match setup", GUILayout.Height(32)))
                {
                    _maps = null;
                    gameController.OpenSetup();
                }
                GUILayout.Space(12);
                GUILayout.EndHorizontal();
            }

            GUILayout.EndArea();
            GUI.matrix = previousMatrix;
        }

        private void DrawSetup(float screenWidth, float screenHeight, float scale)
        {
            if (_maps == null)
            {
                _maps = MapLoader.ListMaps();
                _mapIndex = Mathf.Max(0, Array.IndexOf(_maps, gameController.MapName));
                _players = gameController.PlayerCount;
            }

            float w = Mathf.Min(380, screenWidth - 24);
            var panel = new Rect((screenWidth - w) / 2, (screenHeight - 252) / 2, w, 252);
            _panelPixels = new Rect(panel.position * scale, panel.size * scale);
            GUI.DrawTexture(panel, _panelTex);
            GUILayout.BeginArea(new Rect(panel.x + 18, panel.y + 12, w - 36, panel.height - 20));
            GUILayout.Label("Local match", _title);
            GUILayout.Space(6);
            GUILayout.Label("Map");
            _mapIndex = GUILayout.SelectionGrid(_mapIndex, _maps, 2, GUILayout.Height(28));
            GUILayout.Label("Players");
            _players = GameController.MinPlayers + GUILayout.Toolbar(_players - GameController.MinPlayers,
                new[] { "2", "3", "4" }, GUILayout.Height(28));
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= _players; i++)
            {
                var swatch = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
                GUI.DrawTexture(swatch, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0,
                    SlotView.ColorFor(new PlayerId(i)), 0, 2);
                GUILayout.Label($"P{i}");
            }
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
            if (_setupError != null) GUILayout.Label(_setupError);
            if (_maps.Length > 0 && GUILayout.Button("Start match", GUILayout.Height(35)))
            {
                try
                {
                    gameController.StartLocalMatch(_maps[_mapIndex], _players);
                    _setupError = null;
                }
                catch (Exception ex) { _setupError = ex.Message; }
            }
            GUILayout.EndArea();
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
                    ? "Draw — no winner"
                    : $"Game over — P{gameController.Winner.Value} wins";

            if (gameController.CurrentPhase is BoardWavePhase)
                return "Expanding…";

            if (gameController.IsReviewingHistory) return "Reviewing history";
            if (gameController.IsPaused) return "Paused";
            return $"P{gameController.ActivePlayer.Id.Value} to play · choose a mint tile";
        }

        private void EnsureStyles()
        {
            if (_panelTex != null) return;

            _panelTex = SolidTexture(new Color(0.045f, 0.055f, 0.075f, 1f));
            _activeRowTex = SolidTexture(new Color(1f, 1f, 1f, 0.10f));

            _title = new GUIStyle(GUI.skin.label) { fontSize = 25, fontStyle = FontStyle.Bold };
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
}
