using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Gameplay.Board.View;

namespace WhoWhoo3D.Gameplay.Wave
{
    /// <summary>
    /// Replays a resolved wave. The simulator has already written the final state to the
    /// model, so this rewinds the *views* to the pre-wave state and walks them forward one
    /// chain step at a time. It never writes to BoardModel — the model stays authoritative
    /// throughout, and a skipped or interrupted animation cannot desync the game.
    /// </summary>
    public sealed class WaveAnimator : MonoBehaviour
    {
        [SerializeField] private BoardView boardView;

        [Header("Timing")]
        [Tooltip("Seconds a single chain step takes to play. Raise it to follow long chains.")]
        [Range(0.05f, 3f)]
        [SerializeField] private float stepDuration = 0.9f;
        [Tooltip("Turn off to resolve waves instantly (useful when watching AI play).")]
        [SerializeField] private bool playAnimations = true;

        [Header("Flying tiles")]
        [SerializeField] private float tileSize = 0.18f;
        [SerializeField] private float arcHeight = 0.25f;

        public bool IsPlaying { get; private set; }

        /// <summary>
        /// Holds playback where it is. The phase machine polls <see cref="IsPlaying"/>, so a
        /// paused animation simply keeps BoardWavePhase waiting — no extra plumbing needed.
        /// </summary>
        public bool IsPaused { get; private set; }

        public void Pause() => IsPaused = true;
        public void Resume() => IsPaused = false;

        /// <summary>Lets exactly one more chain step play, then pauses again.</summary>
        public void StepOnce()
        {
            if (!IsPlaying) return;
            _stepAndPause = true;
            IsPaused = false;
        }

        /// <summary>Abandons playback and snaps the views back to the model.</summary>
        public void StopAndSnapToModel()
        {
            StopAllCoroutines();
            IsPlaying = false;
            IsPaused = false;
            _stepAndPause = false;

            if (boardView != null)
            {
                boardView.ClearOverrides();
                boardView.RefreshAll();
            }
        }

        private bool _stepAndPause;

        /// <summary>Seconds one chain step takes. Settable so a speed control can drive it.</summary>
        public float StepDuration
        {
            get => stepDuration;
            set => stepDuration = Mathf.Clamp(value, 0.05f, 3f);
        }

        /// <summary>
        /// False when playback is impossible or pointless — no view, disabled, or outside
        /// Play Mode (coroutines do not run in the editor, and Edit Mode tests rely on
        /// this returning false so waves resolve instantly).
        /// </summary>
        public bool CanPlay =>
            playAnimations && isActiveAndEnabled && Application.isPlaying && ResolveBoardView() != null;

        private void Awake() => ResolveBoardView();

        private BoardView ResolveBoardView()
        {
            if (boardView == null)
                boardView = FindAnyObjectByType<BoardView>();
            return boardView;
        }

        /// <summary>
        /// Starts playback. Poll <see cref="IsPlaying"/> to know when it has finished —
        /// BoardWavePhase does exactly that from its Tick.
        /// </summary>
        public void Play(WaveResult result, BoardModel board)
        {
            if (IsPlaying)
            {
                Debug.LogWarning("WaveAnimator.Play called while already playing — ignoring.");
                return;
            }

            var plan = WavePlaybackPlan.Build(result);
            if (plan.IsEmpty)
                return;

            StartCoroutine(PlayRoutine(plan));
        }

        private IEnumerator PlayRoutine(WavePlaybackPlan plan)
        {
            IsPlaying = true;

            // Rewind the affected sides to how they looked before the simulator ran.
            foreach (var pair in plan.InitialState)
                RequireSlotView(pair.Key).ShowOwner(pair.Value);

            yield return Wait(stepDuration * 0.3f);

            foreach (var frame in plan.Frames)
            {
                yield return PlayFrame(frame, stepDuration);

                foreach (var change in frame.Changes)
                    RequireSlotView(change.Slot).ShowOwner(change.After);

                yield return Wait(stepDuration * 0.2f);

                // A single-step request pauses once the step it asked for has landed.
                if (_stepAndPause)
                {
                    _stepAndPause = false;
                    IsPaused = true;
                }

                yield return WaitWhilePaused();
            }

            // Hand presentation back to the model. Everything the animator showed was a
            // step along the way to the state the board is already in.
            foreach (var pair in plan.InitialState)
                RequireSlotView(pair.Key).ClearOverride();

            IsPlaying = false;
            IsPaused = false;
        }

        /// <summary>WaitForSeconds that respects <see cref="IsPaused"/>.</summary>
        private IEnumerator Wait(float seconds)
        {
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                yield return WaitWhilePaused();
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private IEnumerator WaitWhilePaused()
        {
            while (IsPaused)
                yield return null;
        }

        /// <summary>
        /// Plays one chain step: tiles travel to their destinations while the slots that
        /// spent them drain to white over the same window, so a face visibly empties as its
        /// tiles leave rather than snapping the moment they land.
        /// </summary>
        private IEnumerator PlayFrame(WavePlaybackFrame frame, float duration)
        {
            var flights = frame.Flights;
            if (flights.Count == 0 || duration <= 0f)
                yield break;

            // Slots being spent this step: fade these from their owner's colour to white.
            var draining = new List<(SlotView View, Color From)>();
            foreach (var change in frame.Changes)
            {
                if (change.Cause != SlotChangeCause.Released) continue;
                draining.Add((RequireSlotView(change.Slot), SlotView.ColorFor(change.Before)));
            }

            var projectiles = new List<(Transform Tr, Vector3 From, Vector3 To)>();

            foreach (var flight in flights)
            {
                // Every slot in the model has a view. A missing one means the board and the
                // scene have diverged, which is worth failing on rather than quietly
                // dropping a tile from the animation.
                var from = boardView.GetSlotView(flight.From)
                    ?? throw new InvalidOperationException(
                        $"No view for slot {flight.From}; board and scene are out of sync.");
                var to = boardView.GetSlotView(flight.To)
                    ?? throw new InvalidOperationException(
                        $"No view for slot {flight.To}; board and scene are out of sync.");

                projectiles.Add((
                    CreateProjectile(flight.Owner, from.transform.position),
                    from.transform.position,
                    to.transform.position));
            }

            if (projectiles.Count == 0)
                yield break;

            var neutral = SlotView.NeutralColor;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                yield return WaitWhilePaused();

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;

                foreach (var p in projectiles)
                    if (p.Tr != null)
                        p.Tr.position = Vector3.Lerp(p.From, p.To, t) + Vector3.up * arc;

                // The source drains as its tile travels.
                foreach (var d in draining)
                    d.View.ShowColor(Color.Lerp(d.From, neutral, t));

                yield return null;
            }

            foreach (var p in projectiles)
                if (p.Tr != null)
                    Destroy(p.Tr.gameObject);
        }

        private SlotView RequireSlotView(SlotId id) =>
            boardView.GetSlotView(id)
            ?? throw new InvalidOperationException(
                $"No view for slot {id}; board and scene are out of sync.");

        private Transform CreateProjectile(PlayerId owner, Vector3 position)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"WaveTile_{owner}";
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(transform, worldPositionStays: true);
            go.transform.position = position;
            go.transform.localScale = Vector3.one * tileSize;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = SlotView.SharedMaterial();
            var block = new MaterialPropertyBlock();
            block.SetColor(Shader.PropertyToID("_BaseColor"), SlotView.ColorFor(owner));
            block.SetColor(Shader.PropertyToID("_Color"), SlotView.ColorFor(owner));
            renderer.SetPropertyBlock(block);
            return go.transform;
        }
    }
}
