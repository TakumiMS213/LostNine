using UnityEngine;
using ScenarioSystem.Model;

namespace System_Script.Flow
{
    /// <summary>
    /// Plays a dialogue scenario via MessageWindowManager.
    /// </summary>
    [CreateAssetMenu(fileName = "TalkStep", menuName = "Flow/Steps/Talk Step")]
    public class TalkStep : FlowStep
    {
        public ScenarioData scenario;

        public override void Execute(GameFlowDirector director)
        {
            var facade = ScenarioSystem.Adapter.MessageWindowFacade.Instance;
            if (facade != null && scenario != null)
            {
                facade.StartScenario(scenario, () => 
                {
                    director.NextStep();
                });
            }
            else
            {
                Debug.LogWarning("[TalkStep] MessageWindowFacade or Scenario is missing. Skipping.");
                director.NextStep();
            }
        }
    }
}
