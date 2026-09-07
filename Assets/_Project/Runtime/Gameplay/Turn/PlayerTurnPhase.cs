using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Ai;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Gameplay.Turn
{
    /// <summary>
    /// One player's action. A human turn waits for a click on a cell they own;
    /// an AI turn picks a cell itself after a short "thinking" delay.
    /// Either way the action is +1 tile on the first side the player owns there.
    /// </summary>
    public sealed class PlayerTurnPhase : IGamePhase
    {
        private readonly GameController _game;

        /// <summary>Null when the active player is human.</summary>
        private readonly GreedyAiPlayer _ai;
        private readonly float _aiThinkDelay;
        private float _elapsed;

        public bool IsComplete { get; private set; }
        public bool IsAiTurn => _ai != null;

        public PlayerTurnPhase(GameController game, GreedyAiPlayer ai = null, float aiThinkDelay = 0f)
        {
            _game = game;
            _ai = ai;
            _aiThinkDelay = aiThinkDelay;
        }

        public void Enter()
        {
            // Guard against a soft-lock: a player with no legal move would otherwise
            // never complete this phase and the turn loop would stall forever.
            if (!_game.ActivePlayerHasMove())
            {
                Debug.LogWarning($"{_game.ActivePlayer.Id} has no legal move — skipping turn.");
                _game.NotifyTurnSkipped();
                IsComplete = true;
                return;
            }

            Debug.Log(IsAiTurn
                ? $"--- {_game.ActivePlayer.Id}'s turn (AI thinking) ---"
                : $"--- {_game.ActivePlayer.Id}'s turn (click a white tile) ---");
        }

        public void Tick(float deltaTime)
        {
            if (IsComplete || !IsAiTurn) return;

            _elapsed += deltaTime;
            if (_elapsed < _aiThinkDelay) return;

            ExecuteAiMove();
        }

        /// <summary>Called by GameController when a human clicks a tile.</summary>
        public void HandleSlotClicked(SlotId slotId)
        {
            if (IsComplete || IsAiTurn) return;

            if (_game.TryPlayerAction(slotId))
                IsComplete = true;
        }

        private void ExecuteAiMove()
        {
            if (_ai.TryChooseMove(_game.Board, _game.ActivePlayer.Id, out var slot) &&
                _game.TryPlayerAction(slot))
            {
                Debug.Log($"AI {_game.ActivePlayer.Id} claimed {slot}");
            }
            else
            {
                Debug.LogWarning($"AI {_game.ActivePlayer.Id} found no playable move — passing.");
            }

            IsComplete = true;
        }

        public void Exit() { }

        public IGamePhase NextPhase() => new BoardWavePhase(_game);
    }
}
