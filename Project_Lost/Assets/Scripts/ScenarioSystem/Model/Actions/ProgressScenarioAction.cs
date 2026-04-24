using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// 現在の ProgressManager の状態に応じたシナリオを
    /// データベースから検索してチェーン再生するアクション。
    /// 
    /// ScenarioData のアクションリスト内で ProgressUpdateAction の直後に配置すると、
    /// フェーズ変更 → 次フェーズのシナリオ自動再生、という流れを宣言的に構築できる。
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressScenarioAction", menuName = "Scenario/Actions/Progress Scenario")]
    public class ProgressScenarioAction : ScenarioAction
    {
        public override string ActionType => "ProgressScenario";
    }
}
