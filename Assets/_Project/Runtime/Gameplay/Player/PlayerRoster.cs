using System.Collections.Generic;
using System.Linq;
using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Gameplay.Player;

/// <summary>
/// Manages the list of players and turn order.
/// </summary>
public sealed class PlayerRoster
{
    private readonly List<PlayerState> _players;
    private int _currentIndex;

    public PlayerRoster(List<PlayerState> players)
    {
        _players = players;
        _currentIndex = 0;
    }

    public PlayerState ActivePlayer => _players[_currentIndex];
    public IReadOnlyList<PlayerState> Players => _players;
    internal void RestoreActivePlayer(PlayerId id) =>
        _currentIndex = _players.FindIndex(player => player.Id == id);

    public int AliveCount => _players.Count(p => p.IsAlive);

    /// <summary>
    /// Advances the turn to the next alive player (wrapping around).
    /// </summary>
    public void AdvanceToNextAlive()
    {
        if (AliveCount == 0) return;

        int startIndex = _currentIndex;
        do
        {
            _currentIndex = (_currentIndex + 1) % _players.Count;
        }
        while (!_players[_currentIndex].IsAlive && _currentIndex != startIndex);
    }

    /// <summary>
    /// Recomputes alive status from the board. A player is eliminated once they have
    /// held tiles and lost them all — a player who has not claimed anything yet is
    /// still in the game, which is what makes an empty opening board playable.
    /// </summary>
    public void UpdateAliveStatus(BoardModel board)
    {
        foreach (var player in _players)
        {
            if (player.ComputeScore(board) > 0)
            {
                player.HasHeldTiles = true;
                player.IsAlive = true;
            }
            else if (player.HasHeldTiles)
            {
                player.IsAlive = false;
            }
        }
    }
}

