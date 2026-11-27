using UnityEngine;
using WhoWhoo3D.Domain.Wave;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Gameplay.Turn;

/// <summary>
/// Resolves the wave on the live board, then waits for the animator to finish
/// replaying it. When playback is disabled, the phase completes in Enter().
/// </summary>
public sealed class BoardWavePhase : IGamePhase
{
    private readonly GameController _game;

    public bool IsComplete { get; private set; }
    public WaveResult Result { get; private set; }

    private readonly bool _openingWave;
    public BoardWavePhase(GameController game, bool openingWave = false)
    {
        _openingWave = openingWave;
        _game = game;
    }

    public void Enter()
    {
        // The model is mutated here and now. Animation is presentation catching up to
        // a state the board is already in, never a source of truth.
        Result = WaveSimulator.Simulate(_game.Board, _game.WaveConfig);
        _game.RecordWaveSteps(Result, _game.ActivePlayer.Id);

        if (Result.Steps.Count > 0)
            Debug.Log($"Wave resolved: {Result.Steps.Count} step(s), {Result.TotalChanges} change(s)");

        var animator = _game.WaveAnimator;
        if (Result.Steps.Count == 0 || animator == null || !animator.CanPlay)
        {
            CompleteInstantly();
            return;
        }

        animator.Play(Result, _game.Board);

        // Play() can decline to start (empty plan); don't wait on a coroutine that
        // never began.
        if (!animator.IsPlaying)
            CompleteInstantly();
    }

    public void Tick(float deltaTime)
    {
        if (IsComplete) return;

        var animator = _game.WaveAnimator;
        if (animator == null || !animator.IsPlaying)
            CompleteInstantly();
    }

    private void CompleteInstantly()
    {
        _game.RefreshBoardView();
        IsComplete = true;
    }

    public void Exit() { }

    public IGamePhase NextPhase() => new VictoryCheckPhase(_game, _openingWave);
}

