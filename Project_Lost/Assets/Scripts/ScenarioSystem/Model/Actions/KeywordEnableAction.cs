using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// キーワード機能の有効/無効を切り替えるアクション。
    /// 旧 DialogueScenario.enableKeywords の副作用を明示的なアクションとして分離。
    /// </summary>
    [CreateAssetMenu(fileName = "KeywordEnableAction", menuName = "Scenario/Actions/Keyword Enable")]
    public class KeywordEnableAction : ScenarioAction
    {
        public override string ActionType => "KeywordEnable";

        [Tooltip("キーワード機能を有効にするか")]
        public bool enable = true;
    }
}
