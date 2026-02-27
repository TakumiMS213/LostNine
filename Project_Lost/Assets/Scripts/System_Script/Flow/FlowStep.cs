using UnityEngine;

namespace System_Script.Flow
{
    /// <summary>
    /// Base class for all flow steps in the game loop.
    /// </summary>
    public abstract class FlowStep : ScriptableObject
    {
        /// <summary>
        /// Executes the step logic.
        /// </summary>
        /// <param name="director">The director managing the flow.</param>
        public abstract void Execute(GameFlowDirector director);
    }
}
