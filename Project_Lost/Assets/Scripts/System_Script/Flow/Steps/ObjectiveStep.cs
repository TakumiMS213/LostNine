using UnityEngine;

namespace System_Script.Flow
{
    [CreateAssetMenu(fileName = "ObjectiveStep", menuName = "Flow/Steps/Objective Step")]
    public class ObjectiveStep : FlowStep
    {
        [Tooltip("The text to display as the objective.")]
        [TextArea(2, 5)]
        public string objectiveText;

        [Tooltip("If true, clears the objective (hides it or shows default).")]
        public bool clearObjective = false;

        public override void Execute(GameFlowDirector director)
        {
            if (ObjectiveDisplay.Instance != null)
            {
                if (clearObjective)
                {
                    ObjectiveDisplay.Instance.SetObjectiveText("");
                }
                else
                {
                    ObjectiveDisplay.Instance.SetObjectiveText(objectiveText);
                }
            }
            else
            {
                Debug.LogWarning("[ObjectiveStep] ObjectiveDisplay.Instance is null.");
            }

            director.NextStep();
        }
    }
}
