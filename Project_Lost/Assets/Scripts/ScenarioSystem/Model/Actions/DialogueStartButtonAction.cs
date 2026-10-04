using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>Portrait前面の対話開始ボタンの表示をシナリオから指定する。</summary>
    [CreateAssetMenu(fileName = "DialogueStartButtonAction", menuName = "Scenario/Actions/Dialogue Start Button")]
    public class DialogueStartButtonAction : ScenarioAction
    {
        public override string ActionType => "DialogueStartButton";

        [Tooltip("true: 対話開始ボタンを表示する / false: 非表示にする。操作可否はシナリオの操作許可に従う。")]
        public bool visible = true;
    }
}
