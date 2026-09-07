using UnityEngine;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Gameplay.Turn;

/// <summary>
/// Opening phase of a round. Completes immediately — exists as a hook
/// for future round-start effects (draw cards, passive abilities, etc.).
/// </summary>
public sealed class RoundStartPhase : IGamePhase
{
    private readonly GameController _game;

    public bool IsComplete { get; private set; }

    public RoundStartPhase(GameController game)
    {
        _game = game;
    }

    public void Enter()
    {
        Debug.Log("=== Round Start ===");
        IsComplete = true;
    }

    public void Tick(float deltaTime) { }

    public void Exit() { }

    public IGamePhase NextPhase() => _game.CreatePlayerTurnPhase();
}

