using UnityEngine;

namespace System_Script.Flow
{
    /// <summary>
    /// Updates the global ProgressManager state.
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressStep", menuName = "Flow/Steps/Progress Step")]
    public class ProgressStep : FlowStep
    {
        [Tooltip("The chapter to set.")]
        public int chapter = 1;

        [Tooltip("The phase to set.")]
        public GamePhase phase = GamePhase.Dialogue;

        public override void Execute(GameFlowDirector director)
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.SetProgress(chapter, phase);
            }
            else
            {
                Debug.LogWarning("[ProgressStep] ProgressManager instance is null.");
            }

            // Immediately proceed to next step
            director.NextStep();
        }
    }
}
