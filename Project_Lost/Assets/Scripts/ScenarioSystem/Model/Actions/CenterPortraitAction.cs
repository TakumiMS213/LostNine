using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// 画面中央に常時表示する「センターポートレート」のスプライトを変更するアクション。
    /// メッセージウィンドウ側のポートレートとは独立して管理される。
    /// sprite を null にすると非表示になる。
    /// </summary>
    [CreateAssetMenu(fileName = "CenterPortraitAction", menuName = "Scenario/Actions/Center Portrait")]
    public class CenterPortraitAction : ScenarioAction
    {
        public override string ActionType => "CenterPortrait";

        [Tooltip("表示するスプライト（null で非表示）")]
        public Sprite sprite;
    }
}
