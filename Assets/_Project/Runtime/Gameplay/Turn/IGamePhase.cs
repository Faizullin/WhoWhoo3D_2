namespace WhoWhoo3D.Gameplay.Turn
{
    /// <summary>
    /// A single phase in the turn loop state machine.
    /// GameController calls Tick(deltaTime) while the phase is running and, once
    /// IsComplete is true, calls Exit() then NextPhase() to obtain the successor.
    /// A null successor ends the game.
    /// </summary>
    public interface IGamePhase
    {
        void Enter();

        /// <param name="deltaTime">
        /// Seconds since the previous tick. Passed in rather than read from
        /// UnityEngine.Time so phases stay drivable from Edit Mode tests.
        /// </param>
        void Tick(float deltaTime);

        void Exit();

        bool IsComplete { get; }

        /// <summary>The phase to run next, or null if the game is over.</summary>
        IGamePhase NextPhase();
    }
}
