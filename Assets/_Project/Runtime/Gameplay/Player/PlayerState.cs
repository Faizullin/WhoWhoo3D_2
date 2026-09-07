using WhoWhoo3D.Domain.Board;
using WhoWhoo3D.Gameplay.Board;

namespace WhoWhoo3D.Gameplay.Player;

/// <summary>
/// Represents a single player's state in the game.
/// Score is computed from the board on demand, not cached.
/// </summary>
public sealed class PlayerState
{
    public PlayerId Id { get; }
    public bool IsAlive { get; set; } = true;

    /// <summary>
    /// True once this player has held at least one tile. Elimination is "had tiles and
    /// lost them all" — without this, a game that starts with an empty board would
    /// declare everyone dead on the first victory check.
    /// </summary>
    public bool HasHeldTiles { get; set; }

    /// <summary>When true, GameController plays this player's turns automatically.</summary>
    public bool IsAi { get; set; }

    public PlayerState(PlayerId id)
    {
        Id = id;
    }

    /// <summary>
    /// Computes this player's score by summing all stack counts they own.
    /// </summary>
    public int ComputeScore(BoardModel board) => BoardActions.ScoreFor(board, Id);

    public override string ToString() =>
        $"Player {Id} ({(IsAi ? "AI" : "Human")}, Alive={IsAlive})";
}

