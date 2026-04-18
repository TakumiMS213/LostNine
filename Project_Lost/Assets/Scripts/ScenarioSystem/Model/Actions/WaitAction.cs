using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// 時間待ちアクション。指定秒数の経過後に次のアクションへ進む。
    /// </summary>
    [CreateAssetMenu(fileName = "WaitAction", menuName = "Scenario/Actions/Wait")]
    public class WaitAction : ScenarioAction
    {
        public override string ActionType => "Wait";

        [Tooltip("待機する秒数")]
        public float duration = 1.0f;
    }
}
