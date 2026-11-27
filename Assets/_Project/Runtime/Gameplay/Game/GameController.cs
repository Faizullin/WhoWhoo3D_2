using System;
using System.Collections.Generic;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Gameplay.Ai;
using WhoWhoo3D.Gameplay.Abilities;
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
        public IMatchConnection Connection { get; set; }
        public BoardView RenderedBoard => boardView;
        public MatchAbilities Abilities { get; private set; }
        public MatchAction SelectedAction { get; private set; }
        public SlotId? Target { get; private set; }
        public SlotId? SecondTarget { get; private set; }
        public bool ChoosingRelay => SelectedAction == MatchAction.Relay;
        public SlotId? RelaySource => ChoosingRelay ? Target : null;
        public bool AcceptsBoardInput => !InSetup && !IsGameOver && !IsPaused && !IsReviewingHistory
            && CurrentPhase is PlayerTurnPhase turn && !turn.IsAiTurn && (Connection?.CanPlay(ActivePlayer.Id) ?? true);

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
            if (Board != null) return;
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
            var board = LoadOpeningBoard(setup);

            if (boardView == null)
                boardView = FindAnyObjectByType<BoardView>();
            if (waveAnimator == null)
                waveAnimator = FindAnyObjectByType<WaveAnimator>();

            InitializeMatch(board, boardView, setup);
        }

        public void StartMatchWithoutPresentation(MatchSetup setup) =>
            InitializeMatch(LoadOpeningBoard(setup), null, setup);

        private static BoardModel LoadOpeningBoard(MatchSetup setup)
        {
            var board = MapLoader.Load(setup.MapName);
            foreach (var cell in board.Cells.Values)
                foreach (var side in cell.ExposedSides)
                    foreach (var slot in side.Slots)
                        if (slot.Owner.Value > setup.Seats.Count)
                            board.SetOwner(slot.Id, PlayerId.None);

            return board;
        }

        public void RestoreMatch(MatchSetup setup, MatchSnapshot snapshot)
        {
            var board = LoadOpeningBoard(setup);
            snapshot.RestoreBoard(board, setup.Seats.Count);
            InitializeMatch(board, boardView, setup, snapshot);
        }

        /// <summary>
        /// Initializes the controller with an explicit board and optional view.
        /// Primarily used for testing or custom map loading without file I/O.
        /// </summary>
        public void InitializeWithBoard(BoardModel board, BoardView view = null) =>
            InitializeMatch(board, view, ConfiguredSetup(mapFileName, playerCount));

        private void InitializeMatch(BoardModel board, BoardView view, MatchSetup setup, MatchSnapshot snapshot = null)
        {
            ClearSelection();
            Abilities = new MatchAbilities(this);
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
            if (snapshot == null)
            {
                SetPhase(new RoundStartPhase(this));
                return;
            }

            RestoreTurn(snapshot);
        }

        private void RestoreTurn(MatchSnapshot snapshot)
        {
            TurnNumber = snapshot.turn;
            ConsecutiveSkippedTurns = snapshot.skipped;
            for (int i = 0; i < Roster.Players.Count; i++)
            {
                Roster.Players[i].Inventory = snapshot.HasInventories ? snapshot.inventories[i].Copy() :
                    new AbilityInventory { relay = snapshot.relaySpent == null || snapshot.relaySpent.Length == 0 || !snapshot.relaySpent[i] };
                Roster.Players[i].IsAlive = snapshot.alive[i];
                Roster.Players[i].HasHeldTiles = snapshot.held[i];
            }
            Roster.RestoreActivePlayer(new PlayerId(snapshot.active));
            if (snapshot.finished)
            {
                _currentPhase = null;
                DeclareGameOver(new PlayerId(snapshot.winner));
            }
            else SetPhase(CreatePlayerTurnPhase());
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
            ShowTurnHints(false);
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
        public bool ActivePlayerHasMove()
        {
            foreach (var seat in Roster.Players)
                if (!string.IsNullOrEmpty(seat.Inventory.frozenCell) &&
                    !BoardActions.HasLegalMove(Board, ActivePlayer.Id, new CellId(seat.Inventory.frozenCell)))
                    return false;
            return BoardActions.HasLegalMove(Board, ActivePlayer.Id);
        }

        public bool TryPlayerAction(SlotId slotId)
        {
            if (!AcceptsBoardInput || !Abilities.CanClaim(slotId)) return false;
            return SubmitLocal(new ClaimSlotCommand(ActivePlayer.Id, TurnNumber, slotId));
        }

        private bool SubmitLocal(ClaimSlotCommand command) => Connection?.Submit(command) ?? TrySubmitClaim(command);

        private void ClearSelection()
        {
            SelectedAction = MatchAction.Claim;
            Target = SecondTarget = null;
        }

        public void SelectAbility(MatchAction action)
        {
            ClearSelection();
            if (AcceptsBoardInput && ActivePlayer.Inventory.Has(action)) SelectedAction = action;
            ShowTurnHints(AcceptsBoardInput);
        }

        public void ChooseRelay(bool choose) => SelectAbility(choose ? MatchAction.Relay : MatchAction.Claim);

        public void OnSlotClicked(SlotId slot)
        {
            if (!AcceptsBoardInput) return;
            if (SelectedAction == MatchAction.Claim) { TryPlayerAction(slot); return; }
            if (!Abilities.CanTarget(SelectedAction, slot)) return;
            if (SelectedAction == MatchAction.Beacon && Target is SlotId first && first.Side == slot.Side && first != slot)
                SecondTarget = slot;
            else
            {
                Target = slot;
                SecondTarget = null;
            }
        }

        public bool CanConfirm => AcceptsBoardInput && Target.HasValue &&
            (SelectedAction != MatchAction.Beacon || SecondTarget.HasValue);

        public bool ConfirmAbility()
        {
            if (!CanConfirm) return false;
            var command = new ClaimSlotCommand(ActivePlayer.Id, TurnNumber, Target.Value, SelectedAction,
                SecondTarget?.Edge ?? -1);
            if (!SubmitLocal(command)) return false;
            SelectAbility(MatchAction.Claim);
            return true;
        }

        public bool ConfirmRelay() => ChoosingRelay && ConfirmAbility();

        public bool TakeSupply(MatchAction card) => AcceptsBoardInput && Abilities.CanRefill(card) &&
            SubmitLocal(new ClaimSlotCommand(ActivePlayer.Id, TurnNumber, default, MatchAction.Supply, Refill: card));

        public bool TrySubmitClaim(ClaimSlotCommand command)
        {
            if (InSetup || _gameOver || IsPaused || IsReviewingHistory ||
                _currentPhase is not PlayerTurnPhase turn || turn.IsComplete ||
                command.Actor != ActivePlayer.Id || command.TurnNumber != TurnNumber) return false;

            var before = MatchSnapshot.Capture(this);
            if (!Abilities.TryApply(command, out var kind, out var changes)) return false;
            ClearSelection();
            foreach (var change in changes) Board.SetOwner(change.Slot, change.After);
            string detail = command.Action == MatchAction.Supply ? command.Refill.ToString() : command.Slot.ToString();
            History.Record(new HistoryEntry(kind, TurnNumber, command.Actor,
                $"{command.Actor} {command.Action} {detail}", changes) { BeforeAction = before });
            RefreshBoardView();
            if (command.Action == MatchAction.Supply)
            {
                ShowTurnHints(AcceptsBoardInput);
                return true;
            }
            turn.CompleteClaim();
            NotifyTurnPlayed();
            AdvanceWhileComplete();
            return true;
        }

        internal IGamePhase StartSeatTurn() =>
            Abilities.ResolveBeacon() ? new BoardWavePhase(this, openingWave: true) : CreatePlayerTurnPhase();

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
            int index = History.Cursor + 1;
            if (index >= entries.Count) { ReturnToPresent(); return; }
            // A simulated chain is atomic. Branch at the decision which produced the displayed frames.
            while (index >= 0 && entries[index].BeforeAction == null) index--;
            if (index < 0) return;
            var snapshot = entries[index].BeforeAction;
            while (History.Cursor >= index) History.StepBack();
            History.TruncateAfterCursor();
            waveAnimator?.StopAndSnapToModel();
            boardView?.ClearOverrides();
            snapshot.RestoreBoard(Board, PlayerCount);
            _gameOver = IsPaused = IsReviewingHistory = false;
            Winner = PlayerId.None;
            ClearSelection();
            RestoreTurn(snapshot);
            RefreshBoardView();
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
            if (boardView == null || Board == null) return;
            bool showFaceState = CurrentPhase is PlayerTurnPhase turn && !turn.IsComplete &&
                !InSetup && !IsPaused && !IsGameOver && !IsReviewingHistory;
            boardView.ShowTurnHints(Board, visible && AcceptsBoardInput ? ActivePlayer.Id : PlayerId.None,
                Abilities, SelectedAction, showFaceState);
        }

        public void RefreshBoardView()
        {
            if (boardView != null)
                boardView.RefreshAll();
        }
    }
}
