using UnityEngine;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Networking;
using WhoWhoo3D.UI.Debugging;

namespace WhoWhoo3D.UI.HUD
{
    /// <summary>Scene entry point for setup, live play and results on every map.</summary>
    public sealed class ScoreboardView : MonoBehaviour, IBoardInputPanel
    {
        [SerializeField] private GameController gameController;
        [Tooltip("Extra multiplier on top of the automatic resolution scaling.")]
        [Range(.5f, 4f)] [SerializeField] private float uiScale = 1f;

        private LanMatchSession _session;
        private MatchHud _hud;
        private MatchScreens _screens;
        private TimelineView _timeline;

        private void Awake()
        {
            if (gameController == null)
                gameController = GetComponent<GameController>() ?? FindAnyObjectByType<GameController>();
            _session = GetComponent<LanMatchSession>() ?? gameObject.AddComponent<LanMatchSession>();
            _session.Bind(gameController);
            _timeline = GetComponent<TimelineView>();
            _hud = new MatchHud(transform, gameController, _session);
            _screens = new MatchScreens(transform, gameController, _session);
        }

        private void LateUpdate()
        {
            if (gameController == null || gameController.Roster == null) return;
            bool lobby = _session.State != LanState.Closed && _session.State != LanState.Playing;
            bool playing = !lobby && !gameController.InSetup && !gameController.IsGameOver;
            _hud.SetVisible(playing);
            if (playing)
            {
                _screens.Hide();
                _hud.Refresh(uiScale, _timeline != null && _timeline.IsOpen);
            }
            else _screens.Refresh(uiScale, lobby);
        }

        public bool CoversScreenPoint(Vector2 point) => isActiveAndEnabled &&
            (_screens.Covers(point) || _hud.Covers(point));

        private void OnDisable()
        {
            _screens?.Hide();
            _hud?.SetVisible(false);
        }
    }
}
