using UnityEngine;
using MessageWindowSystem.Data;
using MessageWindowSystem.Core;

namespace System_Script.Flow
{
    /// <summary>
    /// Plays a dialogue scenario via MessageWindowManager.
    /// </summary>
    [CreateAssetMenu(fileName = "TalkStep", menuName = "Flow/Steps/Talk Step")]
    public class TalkStep : FlowStep
    {
        [Tooltip("The scenario to play.")]
        public DialogueScenario scenario;

        public override void Execute(GameFlowDirector director)
        {
            if (MessageWindowManager.Instance != null && scenario != null)
            {
                // Note: implementing Action callback in MessageWindowManager is required for this to work perfectly.
                // For now, we assume StartScenario will be modified to accept a callback.
                
                // Existing StartScenario doesn't have a callback yet. 
                // We will add it in the next step.
                MessageWindowManager.Instance.StartScenario(scenario, () => 
                {
                    director.NextStep();
                });
            }
            else
            {
                Debug.LogWarning("[TalkStep] MessageWindowManager or Scenario is missing. Skipping.");
                director.NextStep();
            }
        }
    }
}
