using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// アニメーションなしでコミュニケーション状態（対話↔探索）を即座に切り替えるアクション。
    /// Portrait の OnClick と同等の処理（形状変更 + ロジック判定）をアニメーションなしで実行する。
    /// </summary>
    [CreateAssetMenu(fileName = "ComuToggleInstantAction", menuName = "Scenario/Actions/Comu Toggle Instant")]
    public class ComuToggleInstantAction : ScenarioAction
    {
        public override string ActionType => "ComuToggleInstant";
    }
}
