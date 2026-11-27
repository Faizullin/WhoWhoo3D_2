using UnityEngine;
using WhoWhoo3D.Gameplay.Board;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Ai;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Gameplay.Turn;

/// <summary>
/// Accepts one claim from the active seat.
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

        _game.ShowTurnHints(!IsAiTurn);
        Debug.Log(IsAiTurn
            ? $"--- {_game.ActivePlayer.Id}'s turn (AI thinking) ---"
            : $"--- {_game.ActivePlayer.Id}'s turn (click a lit tile) ---");
    }

    public void Tick(float deltaTime)
    {
        if (IsComplete || !IsAiTurn) return;

        _elapsed += deltaTime;
        if (_elapsed < _aiThinkDelay) return;

        ExecuteAiMove();
    }

    internal void CompleteClaim() => IsComplete = true;

    private void ExecuteAiMove()
    {
        var actor = _game.ActivePlayer.Id;
        if (!_game.ActivePlayer.RelaySpent)
        {
            foreach (var cell in _game.Board.Cells.Values)
            foreach (var side in cell.ExposedSides)
            foreach (var source in side.Slots)
            {
                if (!BoardActions.TryRelay(_game.Board, source.Id, actor, out var to, out _)) continue;
                var landing = _game.Board.GetSide(to.Side);
                if (landing.OwnedCount(actor) + 2 == landing.SlotCount &&
                    _game.TrySubmitClaim(new ClaimSlotCommand(actor, _game.TurnNumber, source.Id, true))) return;
            }
        }
        if (_ai.TryChooseMove(_game.Board, actor, out var slot))
        {
            _game.TrySubmitClaim(new ClaimSlotCommand(actor, _game.TurnNumber, slot));
            return;
        }

        _game.NotifyTurnSkipped();
        IsComplete = true;
    }

    public void Exit() => _game.ShowTurnHints(false);

    public IGamePhase NextPhase() => new BoardWavePhase(_game);
}

