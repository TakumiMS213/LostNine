using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// コミュニケーション（対話↔探索）の切り替えを行うアクション。
    /// 旧 DialogueScenario.toggleComuOnEnd の副作用を明示的なアクションとして分離。
    /// </summary>
    [CreateAssetMenu(fileName = "ComuToggleAction", menuName = "Scenario/Actions/Comu Toggle")]
    public class ComuToggleAction : ScenarioAction
    {
        public override string ActionType => "ComuToggle";
    }
}
