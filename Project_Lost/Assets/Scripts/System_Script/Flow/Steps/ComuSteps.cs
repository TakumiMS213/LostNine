using UnityEngine;
using Cysharp.Threading.Tasks;

namespace System_Script.Flow
{
    public abstract class ComuStep : FlowStep
    {
        // Base for comu steps
    }

    [CreateAssetMenu(fileName = "ComuStartStep", menuName = "Flow/Steps/Comu Start Step")]
    public class ComuStartStep : ComuStep
    {
        [Tooltip("If true, plays the start animation. If false, instantly sets the state.")]
        public bool allowAnimation = true;

        [Tooltip("The ID to pass to ComuStart (optional).")]
        public string scenarioId;

        public override async void Execute(GameFlowDirector director)
        {
            var manager = FindObjectOfType<ComuStartandEndManager>();
            if (manager != null)
            {
                await manager.ComuStartTask(scenarioId, allowAnimation); 
                director.NextStep();
            }
            else
            {
                Debug.LogWarning("[ComuStartStep] Manager not found.");
                director.NextStep();
            }
        }
    }

    [CreateAssetMenu(fileName = "ComuEndStep", menuName = "Flow/Steps/Comu End Step")]
    public class ComuEndStep : ComuStep
    {
        [Tooltip("If true, plays the end animation. If false, instantly sets the state.")]
        public bool allowAnimation = true;

        [Tooltip("The ID to pass to ComuEnd (optional).")]
        public string scenarioId;

        public override async void Execute(GameFlowDirector director)
        {
            var manager = FindObjectOfType<ComuStartandEndManager>();
            if (manager != null)
            {
                await manager.ComuEndTask(scenarioId, allowAnimation);
                director.NextStep();
            }
            else
            {
                 Debug.LogWarning("[ComuEndStep] Manager not found.");
                director.NextStep();
            }
        }
    }
}
