using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    [CreateAssetMenu(fileName = "PortraitGuidanceAction", menuName = "Scenario/Actions/Portrait Guidance")]
    public class PortraitGuidanceAction : ScenarioAction
    {
        public override string ActionType => "PortraitGuidance";

        [SerializeField] private bool visible = true;

        public bool Visible => visible;
    }
}
