using UnityEngine;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.History;

namespace WhoWhoo3D.UI.Debugging
{
    /// <summary>
    /// Debug timeline: step back and forward through everything that has happened, pause a
    /// wave mid-chain, and optionally rewind the game to where you are looking.
    ///
    /// The arrows are a **viewer** — they change what is drawn, never the board. Only
    /// "Revert here" mutates the game, and it is deliberately a separate button so that
    /// browsing the history can never destroy a game by accident.
    ///
    /// IMGUI so it needs no Canvas or prefab. Editor and development builds only.
    /// </summary>
    public sealed class TimelineView : MonoBehaviour, IBoardInputPanel
    {
        [SerializeField] private GameController gameController;
        [SerializeField] private bool visible = true;

        [Header("Layout")]
        [SerializeField] private float width = 520f;
        [SerializeField] private float bottomMargin = 20f;
        [Range(0.5f, 4f)]
        [SerializeField] private float uiScale = 1f;

        private const float ReferenceHeight = 900f;
        private const float PanelHeight = 108f;

        private Rect _panelPixels;
        public bool CoversScreenPoint(Vector2 point) => isActiveAndEnabled && visible && gameController != null && gameController.Connection == null &&
            _panelPixels.Contains(new Vector2(point.x, Screen.height - point.y));

        private GUIStyle _button;
        private GUIStyle _label;
        private GUIStyle _mono;
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

        private void Update()
        {
            if (gameController == null || gameController.InSetup || gameController.Connection != null) return;

            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.f3Key.wasPressedThisFrame) visible = !visible;
            if (!visible) return;

            if (keyboard.leftArrowKey.wasPressedThisFrame) gameController.StepHistoryBack();
            if (keyboard.rightArrowKey.wasPressedThisFrame) gameController.StepHistoryForward();
            if (keyboard.spaceKey.wasPressedThisFrame) gameController.SetPaused(!gameController.IsPaused);
        }

        private void OnGUI()
        {
            if (!visible || gameController == null || gameController.Board == null || gameController.InSetup || gameController.Connection != null) return;

            EnsureStyles();

            float scale = Mathf.Max(1f, Screen.height / ReferenceHeight) * uiScale;
            var previous = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one * scale);

            float logicalWidth = Screen.width / scale;
            float logicalHeight = Screen.height / scale;
            var area = new Rect((logicalWidth - width) * 0.5f,
                                logicalHeight - PanelHeight - bottomMargin,
                                width, PanelHeight);

            _panelPixels = new Rect(area.position * scale, area.size * scale);
            GUILayout.BeginArea(area, GUIContent.none);
            GUI.DrawTexture(new Rect(0, 0, width, PanelHeight), _panel, ScaleMode.StretchToFill);

            DrawControls();
            DrawStatus();

            GUILayout.EndArea();
            GUI.matrix = previous;
        }

        private void DrawControls()
        {
            var history = gameController.History;
            var animator = gameController.WaveAnimator;
            bool reviewing = gameController.IsReviewingHistory;
            bool paused = gameController.IsPaused;

            GUILayout.Space(10f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(12f);

            if (ActionButton("◀", history.CanStepBack, 52f))
                gameController.StepHistoryBack();

            if (ActionButton("▶", history.CanStepForward, 52f))
                gameController.StepHistoryForward();

            GUILayout.Space(10f);

            // Highlighted while paused, because resuming is then the obvious next action.
            if (ActionButton(paused ? "Resume" : "Pause", enabled: true, 96f, highlighted: paused))
                gameController.SetPaused(!paused);

            bool canStepWave = animator != null && animator.IsPlaying;
            if (ActionButton("Step wave", canStepWave, 110f))
                animator.StepOnce();

            GUILayout.FlexibleSpace();

            // Highlighted while reviewing: getting back to the live game is what you want.
            if (ActionButton("To present", !history.IsAtPresent, 112f, highlighted: reviewing))
                gameController.ReturnToPresent();

            if (ActionButton("Revert here", !history.IsAtPresent, 112f, destructive: true))
                gameController.RevertToCursor();

            GUILayout.Space(12f);
            GUILayout.EndHorizontal();
        }

        /// <summary>
        /// A button that reads clearly in all three states. GUI.enabled alone only halves
        /// the alpha, which is far too subtle on a dark panel — so an unavailable button is
        /// also explicitly dimmed, an important one is tinted, and a destructive one is red.
        /// </summary>
        private bool ActionButton(string label, bool enabled, float width,
                                  bool highlighted = false, bool destructive = false)
        {
            bool previousEnabled = GUI.enabled;
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;

            GUI.enabled = previousEnabled && enabled;

            if (!enabled)
            {
                GUI.backgroundColor = new Color(1f, 1f, 1f, 0.25f);
                GUI.contentColor = new Color(1f, 1f, 1f, 0.30f);
            }
            else if (destructive)
            {
                GUI.backgroundColor = new Color(0.85f, 0.35f, 0.30f);
                GUI.contentColor = Color.white;
            }
            else if (highlighted)
            {
                GUI.backgroundColor = new Color(0.35f, 0.70f, 1f);
                GUI.contentColor = Color.white;
            }
            else
            {
                GUI.contentColor = Color.white;
            }

            bool clicked = GUILayout.Button(label, _button, GUILayout.Width(width), GUILayout.Height(34f));

            GUI.enabled = previousEnabled;
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            return clicked;
        }

        private void DrawStatus()
        {
            var history = gameController.History;

            GUILayout.Space(6f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(14f);

            int position = history.Cursor + 1;
            GUILayout.Label($"{position} / {history.Entries.Count}", _mono, GUILayout.Width(84f));

            string detail;
            if (history.Entries.Count == 0)
                detail = "no moves yet";
            else if (history.Cursor < 0)
                detail = "before the first move";
            else
                detail = history.Entries[history.Cursor].ToString();

            if (gameController.IsReviewingHistory) detail += "   ·   REVIEWING (play is blocked)";
            else if (gameController.IsPaused) detail += "   ·   PAUSED";

            GUILayout.Label(detail, _label);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Space(14f);
            GUILayout.Label(HintLine(), _mono);
            GUILayout.EndHorizontal();
        }

        /// <summary>Explains why the greyed-out buttons are greyed out.</summary>
        private string HintLine()
        {
            var history = gameController.History;

            if (history.Entries.Count == 0)
                return "← → step   ·   space pause   —   nothing recorded yet";

            if (history.IsAtPresent)
                return "← → step   ·   space pause   —   at the present, so nothing to revert";

            return "← → step   ·   space pause   —   reviewing: play is blocked until you return";
        }

        private void EnsureStyles()
        {
            if (_panel != null) return;

            _panel = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            _panel.SetPixel(0, 0, new Color(0.07f, 0.08f, 0.10f, 0.86f));
            _panel.Apply();

            _button = new GUIStyle(GUI.skin.button) { fontSize = 16 };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 14 };
            _label.normal.textColor = Color.white;
            _mono = new GUIStyle(GUI.skin.label) { fontSize = 13 };
            _mono.normal.textColor = new Color(1f, 1f, 1f, 0.55f);
        }

        /// <summary>Restores GUI.enabled even if the body throws.</summary>
        private readonly struct GUIEnabledScope : System.IDisposable
        {
            private readonly bool _previous;

            public GUIEnabledScope(bool enabled)
            {
                _previous = GUI.enabled;
                GUI.enabled = _previous && enabled;
            }

            public void Dispose() => GUI.enabled = _previous;
        }
    }
}
