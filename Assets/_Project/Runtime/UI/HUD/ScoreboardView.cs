using System;
using UnityEngine;
using WhoWhoo3D.Gameplay.Maps;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Turn;
using WhoWhoo3D.Networking;

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

        private const float RowHeight = 30f;
        private const float SwatchSize = 18f;

        public enum Corner { TopLeft, TopRight }

        private LanMatchSession _session;
        private LanRoomPanel _room;
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
                gameController = GetComponent<GameController>() ?? FindAnyObjectByType<GameController>();
            _session = GetComponent<LanMatchSession>() ?? gameObject.AddComponent<LanMatchSession>();
            _session.Bind(gameController);
            _room = new LanRoomPanel(_session);
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
            var viewport = new HudViewport(Screen.safeArea, Screen.height, uiScale);
            var previousMatrix = GUI.matrix;
            GUI.matrix = viewport.Matrix;

            float logicalWidth = viewport.Size.x;
            if (gameController.InSetup || _room.InLobby)
            {
                DrawSetup(viewport);
                GUI.matrix = previousMatrix;
                return;
            }
            int players = gameController.Roster.Players.Count;
            bool inRoom = _session.State == LanState.Playing;
            float height = 34f + players * RowHeight + (gameController.IsGameOver || inRoom ? 112f : 54f);
            if (!gameController.IsGameOver) height += gameController.ChoosingRelay ? 82f : 33f;
            float panelWidth = Mathf.Min(width, logicalWidth - 2 * margin.x);
            float x = corner == Corner.TopLeft ? margin.x : logicalWidth - panelWidth - margin.x;

            var panel = new Rect(x, margin.y, panelWidth, height);
            _panelPixels = viewport.Pixels(panel);
            GUILayout.BeginArea(panel, GUIContent.none);
            GUI.DrawTexture(new Rect(0, 0, panelWidth, height), _panelTex, ScaleMode.StretchToFill);

            GUILayout.Space(8f);
            GUILayout.Label(gameController.IsGameOver ? "MATCH RESULT" : $"TURN {gameController.TurnNumber}", _heading);
            DrawPlayers();
            GUILayout.Space(6f);
            GUILayout.Label(StatusLine(), _note);

            if (!gameController.IsGameOver) DrawRelay();

            if (inRoom)
            {
                GUILayout.BeginHorizontal();
                if (_session.IsHost && gameController.IsGameOver && GUILayout.Button("Play again", GUILayout.Height(30)))
                    _session.Rematch();
                if (GUILayout.Button(_session.IsHost ? "End room" : "Leave room", GUILayout.Height(30))) _session.Leave();
                GUILayout.EndHorizontal();
            }
            else if (gameController.IsGameOver)
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

        private void DrawRelay()
        {
            var seat = gameController.ActivePlayer;
            if (_session.LocalSeat > 0) seat = gameController.Roster.Players[_session.LocalSeat - 1];
            else if (_room.IsSolo) seat = gameController.Roster.Players[0];
            if (!gameController.ChoosingRelay)
            {
                bool enabled = GUI.enabled;
                GUI.enabled = enabled && gameController.AcceptsBoardInput && !seat.RelaySpent;
                if (GUILayout.Button($"Relay · {(seat.RelaySpent ? 0 : 1)} charge", GUILayout.Height(26)))
                    gameController.ChooseRelay(true);
                GUI.enabled = enabled;
                return;
            }
            string prompt = gameController.RelaySource.HasValue
                ? "The boxed tile empties. Both + tiles gain your color. Confirm to spend your turn."
                : "Pick a gold-rimmed tile to move it across its edge and claim two white tiles.";
            if (gameController.RenderedBoard != null && !gameController.RenderedBoard.HasHighlightedTargets)
                prompt = "No owned tile can reach two white tiles. Cancel to make a normal claim.";
            GUILayout.Label(prompt, _note);
            GUILayout.BeginHorizontal();
            bool previous = GUI.enabled;
            GUI.enabled = previous && gameController.AcceptsBoardInput && gameController.RelaySource.HasValue;
            if (GUILayout.Button("Confirm Relay", GUILayout.Height(28))) gameController.ConfirmRelay();
            GUI.enabled = previous;
            if (GUILayout.Button("Cancel", GUILayout.Height(28))) gameController.ChooseRelay(false);
            GUILayout.EndHorizontal();
        }

        private void DrawSetup(HudViewport viewport)
        {
            float screenWidth = viewport.Size.x;
            float screenHeight = viewport.Size.y;
            if (_maps == null)
            {
                _maps = MapLoader.ListMaps();
                _mapIndex = Mathf.Max(0, Array.IndexOf(_maps, gameController.MapName));
                _players = gameController.PlayerCount;
            }

            float w = Mathf.Min(380, screenWidth - 24);
            bool lanSetup = _room.IsHosting || _room.IsJoining || _session.IsBrowsing;
            float height = Mathf.Min(screenHeight - 12, lanSetup ? 390 : 320);
            var panel = new Rect((screenWidth - w) / 2, (screenHeight - height) / 2, w, height);
            _panelPixels = viewport.Pixels(panel);
            GUI.DrawTexture(panel, _panelTex);
            GUILayout.BeginArea(new Rect(panel.x + 18, panel.y + 12, w - 36, panel.height - 20));
            GUILayout.Label(_room.InLobby ? "LAN room" : "Match setup", _title);
            if (_room.InLobby)
            {
                _room.DrawLobby();
                GUILayout.EndArea();
                return;
            }
            _room.DrawModes();
            if (_room.IsJoining)
            {
                _room.DrawJoin();
                GUILayout.EndArea();
                return;
            }
            GUILayout.Space(6);
            GUILayout.Label("Map");
            int columns = Mathf.Clamp(_maps.Length, 1, 3);
            _mapIndex = GUILayout.SelectionGrid(_mapIndex, _maps, columns,
                GUILayout.Height(28 * Mathf.Max(1, Mathf.CeilToInt((float)_maps.Length / columns))));
            GUILayout.Label("Players");
            _players = GameController.MinPlayers + GUILayout.Toolbar(_players - GameController.MinPlayers,
                new[] { "2", "3", "4" }, GUILayout.Height(28));
            GUILayout.BeginHorizontal();
            for (int i = 1; i <= _players; i++)
            {
                var swatch = GUILayoutUtility.GetRect(10, 10, GUILayout.Width(10));
                GUI.DrawTexture(swatch, Texture2D.whiteTexture, ScaleMode.StretchToFill, false, 0,
                    SlotView.ColorFor(new PlayerId(i)), 0, 2);
                GUILayout.Label(_room.IsSolo && i > 1 ? $"CPU {i}" : $"P{i}");
            }
            GUILayout.EndHorizontal();
            if (!_room.IsHosting) GUILayout.Label("Right-drag to orbit · Middle-drag to pan · Scroll to zoom · Home to reset", _note);
            GUILayout.FlexibleSpace();
            if (_setupError != null) GUILayout.Label(_setupError);
            if (_maps.Length > 0 && _room.IsHosting) _room.DrawHost(_maps[_mapIndex], _players);
            else if (_maps.Length > 0 && GUILayout.Button("Start match", GUILayout.Height(35)))
            {
                try
                {
                    var seats = new SeatControl[_players];
                    for (int i = 1; i < seats.Length; i++)
                        seats[i] = _room.IsSolo ? SeatControl.Computer : SeatControl.LocalHuman;
                    gameController.StartMatch(new MatchSetup(_maps[_mapIndex], seats));
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

            if (gameController.ChoosingRelay && gameController.AcceptsBoardInput)
                return $"P{gameController.ActivePlayer.Id.Value} · Relay preview";
            if (_session.State == LanState.Playing)
            {
                string who = $"You are P{_session.LocalSeat}";
                if (gameController.CurrentPhase is BoardWavePhase) return who + " · Expanding…";
                return gameController.ActivePlayer.Id.Value == _session.LocalSeat
                    ? who + " · Your turn" : who + $" · Waiting for P{gameController.ActivePlayer.Id.Value}";
            }
            if (gameController.CurrentPhase is BoardWavePhase)
                return "Expanding…";

            if (gameController.IsReviewingHistory) return "Reviewing history";
            if (gameController.IsPaused) return "Paused";
            if (gameController.ActivePlayer.IsAi) return $"P{gameController.ActivePlayer.Id.Value} is choosing a move…";
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
