using System.Linq;
using UnityEngine;
using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Game;

namespace WhoWhoo3D.Gameplay.Turn
{
    /// <summary>
    /// Checks whether the game is over (at most one player still holds tiles).
    /// Exit() commits the outcome: either declares the winner or hands the turn
    /// to the next living player.
    /// </summary>
    public sealed class VictoryCheckPhase : IGamePhase
    {
        private readonly GameController _game;

        public bool IsComplete { get; private set; }
        public bool GameOver { get; private set; }
        public PlayerId Winner { get; private set; }

        public VictoryCheckPhase(GameController game)
        {
            _game = game;
        }

        public void Enter()
        {
            _game.Roster.UpdateAliveStatus(_game.Board);

            if (_game.Roster.AliveCount <= 1)
            {
                GameOver = true;
                var winner = _game.Roster.Players.FirstOrDefault(p => p.IsAlive);
                Winner = winner?.Id ?? PlayerId.None;
                Debug.Log($"=== GAME OVER === Winner: {Winner}");
            }
            else if (_game.EveryoneIsStuck)
            {
                // A full round has passed with nobody able to move — the board is saturated
                // or every face is locked. Decide it on tiles held rather than leaving the
                // phase machine to skip turns until its transition cap trips.
                GameOver = true;
                Winner = _game.LeadingPlayer();
                Debug.Log($"=== GAME OVER (stalemate) === Winner: {Winner}");
            }

            IsComplete = true;
        }

        public void Tick(float deltaTime) { }

        public void Exit()
        {
            if (GameOver)
            {
                _game.DeclareGameOver(Winner);
            }
            else
            {
                _game.Roster.AdvanceToNextAlive();
                _game.BeginNextTurn();
            }
        }

        public IGamePhase NextPhase() => GameOver ? null : _game.CreatePlayerTurnPhase();
    }
}
