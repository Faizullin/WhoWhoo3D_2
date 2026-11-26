using System;
using System.Collections.Generic;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Gameplay.Ai;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Gameplay.Board.View;
using WhoWhoo3D.Gameplay.History;
using WhoWhoo3D.Gameplay.Maps;
using WhoWhoo3D.Gameplay.Player;
using WhoWhoo3D.Gameplay.Turn;
using WhoWhoo3D.Gameplay.Wave;

namespace WhoWhoo3D.Gameplay.Game
{
    /// <summary>
    /// Top-level game orchestrator. Owns the BoardModel, PlayerRoster,
    /// and drives the phase state machine. One per scene, not static.
    /// </summary>
    public class GameController : MonoBehaviour
    {
        /// <summary>
        /// Ceiling on how many phases may complete within a single advance.
        /// A correct turn (turn → wave → victory → next turn) uses about four;
        /// anything beyond this means two phases are completing each other in a
        /// cycle, so we stop rather than hang the editor.
        /// </summary>
        private const int MaxPhaseChain = 64;

        public const int MinPlayers = MatchSetup.MinPlayers;
        public const int MaxPlayers = MatchSetup.MaxPlayers;

        [Header("Board")]
        [SerializeField] private BoardView boardView;
        [SerializeField] private WaveAnimator waveAnimator;
        [SerializeField] private string mapFileName = "sample";
        [SerializeField] private bool showSetupOnStart;

        public MatchSetup Setup { get; private set; }
        public string MapName => Setup?.MapName ?? mapFileName;
        public int PlayerCount => Setup?.Seats.Count ?? playerCount;
        public bool InSetup { get; private set; }
        public bool AcceptsBoardInput => !InSetup && !IsGameOver && !IsPaused && !IsReviewingHistory
            && CurrentPhase is PlayerTurnPhase turn && !turn.IsAiTurn;

        [Header("Players")]
        [Tooltip("How many people are playing on this machine, hot-seat.")]
        [Range(MinPlayers, MaxPlayers)]
        [SerializeField] private int playerCount = 2;

        [Header("Seat defaults")]
        [SerializeField] private bool enableAi;
        [SerializeField] private int humanPlayerId = 1;
        [SerializeField] private float aiThinkDelay = 0.5f;

        public BoardModel Board { get; private set; }
        public PlayerRoster Roster { get; private set; }
        public WaveConfig WaveConfig => WaveConfig.Default;
        public PlayerState ActivePlayer => Roster.ActivePlayer;
        public IGamePhase CurrentPhase => _currentPhase;
        public bool IsGameOver => _gameOver;
        public PlayerId Winner { get; private set; }

        /// <summary>Null when no animator is present — waves then resolve instantly.</summary>
        public WaveAnimator WaveAnimator => waveAnimator;

        /// <summary>Everything that has happened, and the cursor for reviewing it.</summary>
        public GameHistory History { get; } = new();

        /// <summary>Turn number, 1-based. Groups history entries.</summary>
        public int TurnNumber { get; private set; }

        /// <summary>
        /// While true the phase machine stops advancing. Used by the debug timeline so a
        /// wave can be inspected mid-chain without the game running on underneath it.
        /// </summary>
        public bool IsPaused { get; private set; }

        /// <summary>True while the view is showing a past state rather than the live board.</summary>
        public bool IsReviewingHistory { get; private set; }

        /// <summary>
        /// Turns skipped back-to-back because the player had no legal move. Once this
        /// reaches the player count, a full round has passed with nobody able to act and
        /// the game must be ended rather than left spinning.
        /// </summary>
        public int ConsecutiveSkippedTurns { get; private set; }

        /// <summary>True when a whole round has gone by with no player able to move.</summary>
        public bool EveryoneIsStuck =>
            Roster != null && ConsecutiveSkippedTurns >= Roster.Players.Count;

        private IGamePhase _currentPhase;
        private GreedyAiPlayer _ai;
        private bool _gameOver;

        private void Start()
        {
            StartLocalMatch(mapFileName, playerCount);
            if (showSetupOnStart) OpenSetup();
        }

        private void Update()
        {
            Tick(Time.deltaTime);
        }

        // ── Initialization ───────────────────────────────────────────

        public void OpenSetup()
        {
            InSetup = true;
            ShowTurnHints(false);
        }

        public void RestartMatch() => StartMatch(Setup ?? ConfiguredSetup(mapFileName, playerCount));

        public void StartLocalMatch(string map, int players) => StartMatch(ConfiguredSetup(map, players));

        private MatchSetup ConfiguredSetup(string map, int players)
        {
            var seats = new SeatControl[Mathf.Clamp(players, MinPlayers, MaxPlayers)];
            for (int i = 0; i < seats.Length; i++)
                seats[i] = enableAi && i + 1 != humanPlayerId ? SeatControl.Computer : SeatControl.LocalHuman;
            return new MatchSetup(map, seats);
        }

        public void StartMatch(MatchSetup setup)
        {
            if (setup == null) throw new ArgumentNullException(nameof(setup));
            var board = MapLoader.Load(setup.MapName);
            foreach (var cell in board.Cells.Values)
                foreach (var side in cell.ExposedSides)
                    foreach (var slot in side.Slots)
                        if (slot.Owner.Value > setup.Seats.Count)
                            board.SetOwner(slot.Id, PlayerId.None);

            if (boardView == null)
                boardView = FindAnyObjectByType<BoardView>();
            if (waveAnimator == null)
                waveAnimator = FindAnyObjectByType<WaveAnimator>();

            InitializeMatch(board, boardView, setup);
            Debug.Log($"Game initialized: {Roster.Players.Count} players on map '{setup.MapName}'");
        }

        /// <summary>
        /// Initializes the controller with an explicit board and optional view.
        /// Primarily used for testing or custom map loading without file I/O.
        /// </summary>
        public void InitializeWithBoard(BoardModel board, BoardView view = null) =>
            InitializeMatch(board, view, ConfiguredSetup(mapFileName, playerCount));

        private void InitializeMatch(BoardModel board, BoardView view, MatchSetup setup)
        {
            Setup = setup;
            waveAnimator?.StopAndSnapToModel();
            InSetup = false;
            Board = board;
            _gameOver = false;
            Winner = PlayerId.None;

            if (view != null)
            {
                boardView = view;
                boardView.Render(Board);
            }

            History.Clear();
            TurnNumber = 1;
            IsPaused = false;
            IsReviewingHistory = false;
            ConsecutiveSkippedTurns = 0;

            Roster = BuildRoster();
            SetPhase(new RoundStartPhase(this));
        }

        /// <summary>Sets how many people are playing. Call before InitializeWithBoard.</summary>
        public void ConfigurePlayers(int count)
        {
            playerCount = Mathf.Clamp(count, MinPlayers, MaxPlayers);
        }

        public void ConfigureAi(bool enabled, int humanPlayer, float thinkDelay = 0f)
        {
            enableAi = enabled;
            humanPlayerId = humanPlayer;
            aiThinkDelay = thinkDelay;
            _ai = null;
        }

        /// <summary>
        /// Builds the roster from the configured player count. Players are a setup choice,
        /// not something inferred from whichever tiles a map happens to seed — that older
        /// behaviour made an all-white opening board impossible to start.
        /// </summary>
        private PlayerRoster BuildRoster()
        {
            int count = Setup.Seats.Count;

            var states = new List<PlayerState>(count);
            for (int i = 1; i <= count; i++)
            {
                var id = new PlayerId(i);
                states.Add(new PlayerState(id) { IsAi = Setup.Seats[i - 1] == SeatControl.Computer });
            }

            return new PlayerRoster(states);
        }

        // ── Phase Machine ────────────────────────────────────────────

        /// <summary>
        /// Advances the simulation by one step. Called from Update() at runtime and
        /// directly from Edit Mode tests, which have no frame loop.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (InSetup || _currentPhase == null || _gameOver || IsPaused || IsReviewingHistory) return;

            _currentPhase.Tick(deltaTime);
            AdvanceWhileComplete();
        }

        /// <summary>Builds the turn phase for the active seat.</summary>
        public PlayerTurnPhase CreatePlayerTurnPhase()
        {
            if (!ActivePlayer.IsAi)
                return new PlayerTurnPhase(this);

            _ai ??= new GreedyAiPlayer(WaveConfig);
            return new PlayerTurnPhase(this, _ai, aiThinkDelay);
        }

        public void DeclareGameOver(PlayerId winner)
        {
            _gameOver = true;
            Winner = winner;
        }

        private void SetPhase(IGamePhase phase)
        {
            _currentPhase = phase;
            _currentPhase.Enter();
            AdvanceWhileComplete();
        }

        /// <summary>
        /// Drains every phase that is already complete. Iterative rather than
        /// recursive so a misbehaving phase cannot blow the stack, and bounded by
        /// MaxPhaseChain so it cannot spin forever.
        /// </summary>
        private void AdvanceWhileComplete()
        {
            int steps = 0;

            while (_currentPhase != null && _currentPhase.IsComplete)
            {
                if (++steps > MaxPhaseChain)
                {
                    Debug.LogError(
                        $"Phase machine exceeded {MaxPhaseChain} transitions in one advance " +
                        $"(stuck on {_currentPhase.GetType().Name}). Halting to avoid a freeze.");
                    _currentPhase = null;
                    return;
                }

                var finished = _currentPhase;
                finished.Exit();

                var next = finished.NextPhase();
                _currentPhase = next;

                if (next == null)
                {
                    Debug.Log($"Game ended. Winner: {Winner}");
                    return;
                }

                next.Enter();
            }
        }

        // ── Player Actions ───────────────────────────────────────────

        /// <summary>True if the active player has at least one cell they can reinforce.</summary>
        public bool ActivePlayerHasMove() => BoardActions.HasLegalMove(Board, ActivePlayer.Id);

        /// <summary>Local pointer entry point.</summary>
        public bool TryPlayerAction(SlotId slotId) =>
            AcceptsBoardInput && TrySubmitClaim(new ClaimSlotCommand(ActivePlayer.Id, TurnNumber, slotId));

        public void OnSlotClicked(SlotId slotId) => TryPlayerAction(slotId);

        /// <summary>Accepts at most one claim per turn. Called on the Unity main thread.</summary>
        public bool TrySubmitClaim(ClaimSlotCommand command)
        {
            if (InSetup || _gameOver || IsPaused || IsReviewingHistory ||
                _currentPhase is not PlayerTurnPhase turn || turn.IsComplete ||
                command.Actor != ActivePlayer.Id || command.TurnNumber != TurnNumber ||
                !BoardActions.CanClaim(Board, command.Slot, command.Actor))
                return false;

            turn.CompleteClaim();
            Board.SetOwner(command.Slot, command.Actor);
            NotifyTurnPlayed();
            History.Record(new HistoryEntry(
                HistoryKind.Claim, TurnNumber, command.Actor, $"{command.Actor} claimed {command.Slot}",
                new[] { new SlotStateChange(command.Slot, PlayerId.None, command.Actor, SlotChangeCause.Claimed, Origin: null) }));

            RefreshBoardView();
            Debug.Log($"{command.Actor} claimed {command.Slot}");
            AdvanceWhileComplete();
            return true;
        }

        // ── Presentation State ───────────────────────────────────────

        /// <summary>
        /// Tiles a player holds **as currently drawn**. While a wave is animating this
        /// follows the frame on screen; otherwise it is the model.
        ///
        /// The model is mutated to its final state before the first frame plays, so
        /// anything showing the player's score has to read this rather than the board —
        /// otherwise the numbers jump to the outcome while the cubes are still mid-chain,
        /// and a player who is wiped out partway through a chain is not shown as out until
        /// the whole wave finishes.
        /// </summary>
        public int DisplayScoreFor(PlayerId player)
        {
            if (waveAnimator != null && waveAnimator.IsPlaying &&
                waveAnimator.TryGetDisplayScore(player, out int animated))
                return animated;

            return BoardActions.ScoreFor(Board, player);
        }

        /// <summary>
        /// True when the player is shown as eliminated. Based on what is drawn, so it flips
        /// the moment their last tile is taken on screen — not when the wave ends.
        ///
        /// This is presentation only. Authoritative elimination still happens in
        /// VictoryCheckPhase, once the board has settled.
        /// </summary>
        public bool DisplayIsOut(PlayerState player) =>
            player.HasHeldTiles && DisplayScoreFor(player.Id) == 0;

        // ── History / Debug Timeline ─────────────────────────────────

        /// <summary>Records one chain step of a wave. Called by BoardWavePhase.</summary>
        public void RecordWaveSteps(WaveResult result, PlayerId actor)
        {
            for (int i = 0; i < result.Steps.Count; i++)
            {
                var step = result.Steps[i];
                if (step.Changes.Count == 0) continue;

                History.Record(new HistoryEntry(
                    HistoryKind.WaveStep, TurnNumber, actor,
                    $"expansion step {i + 1}/{result.Steps.Count}",
                    step.Changes.ToArray()));
            }
        }

        /// <summary>Bumps the turn counter so history entries group by turn.</summary>
        public void BeginNextTurn() => TurnNumber++;

        /// <summary>Called by PlayerTurnPhase when a player could not act.</summary>
        public void NotifyTurnSkipped() => ConsecutiveSkippedTurns++;

        /// <summary>Called when a player actually moved, clearing the stall counter.</summary>
        public void NotifyTurnPlayed() => ConsecutiveSkippedTurns = 0;

        /// <summary>The player holding the most tiles, or None on a tie.</summary>
        public PlayerId LeadingPlayer()
        {
            var leader = PlayerId.None;
            int best = 0;
            bool tied = false;

            foreach (var player in Roster.Players)
            {
                int score = player.ComputeScore(Board);
                if (score > best) { best = score; leader = player.Id; tied = false; }
                else if (score == best && score > 0) tied = true;
            }

            return tied ? PlayerId.None : leader;
        }

        public void SetPaused(bool paused)
        {
            IsPaused = paused;
            ShowTurnHints(AcceptsBoardInput);

            if (waveAnimator == null) return;
            if (paused || IsReviewingHistory) waveAnimator.Pause();
            else waveAnimator.Resume();
        }

        /// <summary>
        /// Moves the *view* back one entry. The board is untouched — this is a viewer, not
        /// an undo. Use <see cref="RevertToCursor"/> to actually rewind the game.
        /// </summary>
        public bool StepHistoryBack()
        {
            var entry = History.StepBack();
            if (entry == null) return false;

            EnterReviewMode();
            foreach (var change in entry.Changes)
                boardView?.ShowOwner(change.Slot, change.Before);

            return true;
        }

        public bool StepHistoryForward()
        {
            var entry = History.StepForward();
            if (entry == null) return false;

            foreach (var change in entry.Changes)
                boardView?.ShowOwner(change.Slot, change.After);

            if (History.IsAtPresent)
                ReturnToPresent();

            return true;
        }

        /// <summary>Fast-forwards the view to the live board and resumes play.</summary>
        public void ReturnToPresent()
        {
            while (History.StepForward() != null) { }

            IsReviewingHistory = false;
            if (!IsPaused) waveAnimator?.Resume();
            ShowTurnHints(AcceptsBoardInput);
            boardView?.ClearOverrides();
            RefreshBoardView();
        }

        /// <summary>
        /// Actually rewinds the game to the cursor: reverses every entry after it on the
        /// live board and drops them. Destructive, and deliberately separate from the
        /// stepping arrows so reviewing can never lose a game by accident.
        /// </summary>
        public void RevertToCursor()
        {
            var entries = History.Entries;

            for (int i = entries.Count - 1; i > History.Cursor; i--)
                foreach (var change in entries[i].Changes)
                    Board.SetOwner(change.Slot, change.Before);

            History.TruncateAfterCursor();

            _gameOver = false;
            Winner = PlayerId.None;
            IsReviewingHistory = false;
            IsPaused = false;

            waveAnimator?.StopAndSnapToModel();
            boardView?.ClearOverrides();
            RefreshBoardView();

            Roster.UpdateAliveStatus(Board);
            SetPhase(CreatePlayerTurnPhase());
        }

        private void EnterReviewMode()
        {
            if (IsReviewingHistory) return;

            IsReviewingHistory = true;
            ShowTurnHints(false);
            waveAnimator?.Pause();
        }

        // ── View Sync ────────────────────────────────────────────────

        public void ShowTurnHints(bool visible)
        {
            if (boardView != null && Board != null)
                boardView.ShowTurnHints(Board, visible ? ActivePlayer.Id : PlayerId.None);
        }

        public void RefreshBoardView()
        {
            if (boardView != null)
                boardView.RefreshAll();
        }
    }
}
